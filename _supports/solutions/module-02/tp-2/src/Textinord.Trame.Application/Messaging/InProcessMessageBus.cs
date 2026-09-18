using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Textinord.Trame.Application.Messaging;

/// <summary>
/// Bus in-process : un Channel borné par canal, une boucle de consommation par canal,
/// dispatch séquentiel aux handlers abonnés. Expiration et exceptions partent en dead letter,
/// la boucle continue. Suffisant pour le développement, les tests et la démonstration ;
/// remplacé par un broker au module 6.
/// </summary>
public sealed partial class InProcessMessageBus : IMessageBus, IDisposable, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, MessageChannel> _canaux = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, List<MessageHandler>> _abonnements = new(StringComparer.Ordinal);
    private readonly List<string> _ordreDeclaration = [];
    private readonly Dictionary<string, Task> _consommateurs = new(StringComparer.Ordinal);
    private readonly Lock _verrou = new();
    private readonly DeadLetterChannel _deadLetters;
    private readonly TimeProvider _horloge;
    private readonly ILogger<InProcessMessageBus> _logger;
    private CancellationTokenSource? _arret;
    private bool _demarre;
    private bool _arrete;

    public InProcessMessageBus(
        DeadLetterChannel deadLetters,
        TimeProvider? horloge = null,
        ILogger<InProcessMessageBus>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(deadLetters);

        _deadLetters = deadLetters;
        _horloge = horloge ?? TimeProvider.System;
        _logger = logger ?? NullLogger<InProcessMessageBus>.Instance;
    }

    public IReadOnlyCollection<string> Canaux
    {
        get
        {
            lock (_verrou)
            {
                return _ordreDeclaration.ToArray();
            }
        }
    }

    public DeadLetterChannel DeadLetters => _deadLetters;

    public MessageChannel DeclarerCanal(string nom, int capacite = MessageChannel.CapaciteParDefaut)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nom);

        lock (_verrou)
        {
            if (_demarre)
            {
                throw new InvalidOperationException("Impossible de déclarer un canal après le démarrage du bus.");
            }

            return _canaux.GetOrAdd(nom, n =>
            {
                _ordreDeclaration.Add(n);
                return new MessageChannel(n, capacite);
            });
        }
    }

    public IMessageBus Abonner(string canal, MessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        DeclarerCanal(canal);

        lock (_verrou)
        {
            _abonnements.GetOrAdd(canal, _ => []).Add(handler);
        }

        return this;
    }

    public ValueTask PublierAsync<T>(
        string canal,
        T corps,
        Guid? correlationId = null,
        TimeSpan? dureeDeVie = null,
        CancellationToken cancellationToken = default)
        where T : notnull
    {
        var message = MessageEnvelope.Creer(corps, _horloge.GetUtcNow(), correlationId, dureeDeVie);
        return PublierEnveloppeAsync(canal, message, cancellationToken);
    }

    public ValueTask PublierEnveloppeAsync(string canal, MessageEnvelope message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canal);
        ArgumentNullException.ThrowIfNull(message);

        if (!_canaux.TryGetValue(canal, out var cible))
        {
            throw CanalInconnuException.Pour(canal);
        }

        return cible.Redacteur.WriteAsync(message, cancellationToken);
    }

    public Task DemarrerAsync(CancellationToken cancellationToken = default)
    {
        lock (_verrou)
        {
            if (_demarre)
            {
                return Task.CompletedTask;
            }

            _demarre = true;
            _arret = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            foreach (var nom in _ordreDeclaration)
            {
                var canal = _canaux[nom];
                var handlers = _abonnements.TryGetValue(nom, out var liste) ? liste.ToArray() : [];
                _consommateurs[nom] = Task.Run(() => ConsommerAsync(canal, handlers, _arret.Token), CancellationToken.None);
            }

            JournaliserDemarrage(_ordreDeclaration.Count);
        }

        return Task.CompletedTask;
    }

    public async Task ArreterAsync(CancellationToken cancellationToken = default)
    {
        string[] ordre;

        lock (_verrou)
        {
            if (!_demarre || _arrete)
            {
                return;
            }

            _arrete = true;
            ordre = _ordreDeclaration.ToArray();
        }

        // Fermeture amont → aval : un canal n'est fermé qu'une fois ses producteurs vidés,
        // les messages dérivés en cours de traitement trouvent donc toujours leur canal ouvert.
        foreach (var nom in ordre)
        {
            _canaux[nom].Redacteur.TryComplete();
            await _consommateurs[nom].WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        JournaliserArret(_deadLetters.Nombre);
    }

    public void Dispose()
    {
        _arret?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await ArreterAsync().ConfigureAwait(false);
        Dispose();
    }

    private async Task ConsommerAsync(MessageChannel canal, MessageHandler[] handlers, CancellationToken cancellationToken)
    {
        await foreach (var message in canal.Lecteur.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            var maintenant = _horloge.GetUtcNow();

            if (message.EstExpire(maintenant))
            {
                _deadLetters.Deposer(message, canal.Nom, $"Message expiré le {message.ExpireLe:O}", maintenant);
                continue;
            }

            if (handlers.Length == 0)
            {
                _deadLetters.Deposer(message, canal.Nom, "Aucun abonné sur ce canal", maintenant);
                continue;
            }

            var contexte = new MessageContext(this, canal.Nom, message);

            foreach (var handler in handlers)
            {
                try
                {
                    await handler(message, contexte, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    JournaliserEchec(ex, message.MessageId, canal.Nom);
                    _deadLetters.Deposer(message, canal.Nom, $"{ex.GetType().Name} : {ex.Message}", _horloge.GetUtcNow());
                }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Bus in-process démarré : {NombreCanaux} canaux")]
    private partial void JournaliserDemarrage(int nombreCanaux);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bus in-process arrêté : {NombreDeadLetters} dead letters")]
    private partial void JournaliserArret(int nombreDeadLetters);

    [LoggerMessage(Level = LogLevel.Error, Message = "Échec du traitement du message {MessageId} sur « {Canal} »")]
    private partial void JournaliserEchec(Exception exception, Guid messageId, string canal);

    private sealed class MessageContext(InProcessMessageBus bus, string canal, MessageEnvelope message) : IMessageContext
    {
        public MessageEnvelope Message => message;

        public string Canal => canal;

        public DateTimeOffset Maintenant => bus._horloge.GetUtcNow();

        public ValueTask PublierAsync<T>(string canalCible, T corps, CancellationToken cancellationToken = default)
            where T : notnull =>
            bus.PublierEnveloppeAsync(canalCible, message.Deriver(corps, Maintenant), cancellationToken);

        public ValueTask PublierEnveloppeAsync(string canalCible, MessageEnvelope enveloppe, CancellationToken cancellationToken = default) =>
            bus.PublierEnveloppeAsync(canalCible, enveloppe, cancellationToken);

        public ValueTask RejeterAsync(string raison, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bus._deadLetters.Deposer(message, canal, raison, Maintenant);
            return ValueTask.CompletedTask;
        }
    }
}
