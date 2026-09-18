using System.Collections.Concurrent;

namespace Textinord.Trame.Application.Messaging;

public sealed record MessageJournalise(string Canal, MessageEnvelope Message, DateTimeOffset VuLe);

/// <summary>
/// Message Store : conserve une copie des messages qui passent sur les canaux audités,
/// consultable par corrélation. C'est le journal que Marc Vandewalle demande quand un ordre
/// de préparation « n'est jamais arrivé ».
/// </summary>
public sealed class HistoriqueMessages
{
    public const int CapaciteParDefaut = 10_000;

    private readonly ConcurrentQueue<MessageJournalise> _messages = new();
    private readonly int _capacite;

    public HistoriqueMessages(int capacite = CapaciteParDefaut)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacite);
        _capacite = capacite;
    }

    public IReadOnlyList<MessageJournalise> Messages => _messages.ToArray();

    public void Enregistrer(string canal, MessageEnvelope message, DateTimeOffset maintenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canal);
        ArgumentNullException.ThrowIfNull(message);

        _messages.Enqueue(new MessageJournalise(canal, message, maintenant));

        while (_messages.Count > _capacite && _messages.TryDequeue(out _))
        {
            // On garde les N derniers messages : un magasin de messages borné.
        }
    }

    public IReadOnlyList<MessageJournalise> ParCorrelation(Guid correlationId) =>
        _messages.Where(m => m.Message.CorrelationId == correlationId)
                 .OrderBy(m => m.Message.EmisLe)
                 .ThenBy(m => m.Message.NumeroSequence ?? 0)
                 .ThenBy(m => m.Message.MessageId)
                 .ToList();

    /// <summary>Handler prêt à abonner sur un canal d'audit.</summary>
    public Task EnregistrerAsync(MessageEnvelope message, IMessageContext contexte, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contexte);
        cancellationToken.ThrowIfCancellationRequested();

        Enregistrer(contexte.Canal, message, contexte.Maintenant);
        return Task.CompletedTask;
    }
}
