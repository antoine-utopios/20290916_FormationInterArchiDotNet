using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Textinord.Trame.Application.Messaging;

public sealed record DeadLetter(MessageEnvelope Message, string Canal, string Raison, DateTimeOffset DeposeeLe);

/// <summary>
/// Dead Letter Channel : reçoit les messages expirés, invalides ou dont le traitement a échoué.
/// Rien n'est perdu, tout est lisible (supervision, rejeu manuel, tests).
/// </summary>
public sealed partial class DeadLetterChannel : IDisposable
{
    public const string Nom = "dead-letter";

    private readonly ConcurrentQueue<DeadLetter> _lettres = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly ILogger<DeadLetterChannel> _logger;

    public DeadLetterChannel(ILogger<DeadLetterChannel>? logger = null)
    {
        _logger = logger ?? NullLogger<DeadLetterChannel>.Instance;
    }

    public int Nombre => _lettres.Count;

    public IReadOnlyList<DeadLetter> Lettres => _lettres.ToArray();

    public void Deposer(MessageEnvelope message, string canal, string raison, DateTimeOffset maintenant)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(canal);
        ArgumentException.ThrowIfNullOrWhiteSpace(raison);

        _lettres.Enqueue(new DeadLetter(message, canal, raison, maintenant));
        JournaliserDepot(message.MessageId, message.Type, canal, raison);
        _signal.Release();
    }

    /// <summary>Attend la prochaine dead letter (utile en test et en supervision).</summary>
    public async Task<DeadLetter> AttendreAsync(TimeSpan delai, CancellationToken cancellationToken = default)
    {
        if (!await _signal.WaitAsync(delai, cancellationToken).ConfigureAwait(false))
        {
            throw new TimeoutException($"Aucune dead letter reçue en {delai.TotalSeconds:F0} s.");
        }

        return _lettres.ToArray()[^1];
    }

    public void Dispose() => _signal.Dispose();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dead letter : message {MessageId} ({Type}) depuis « {Canal} » : {Raison}")]
    private partial void JournaliserDepot(Guid messageId, string type, string canal, string raison);
}
