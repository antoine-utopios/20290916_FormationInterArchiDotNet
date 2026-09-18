using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Textinord.Trame.Application.Commandes;
using Textinord.Trame.Application.Commandes.Messages;
using Textinord.Trame.Domain.Entrepots;

namespace Textinord.Trame.Infrastructure.Entrepots;

public sealed record OrdreTransmis(Entrepot Entrepot, OrdrePreparationEntrepot Ordre, DateTimeOffset TransmisLe);

/// <summary>Passerelle entrepôt de développement : journalise et conserve les ordres transmis.</summary>
public sealed partial class JournalEntrepotGateway(ILogger<JournalEntrepotGateway> logger, TimeProvider horloge) : IEntrepotGateway
{
    private readonly ConcurrentQueue<OrdreTransmis> _ordres = new();

    public IReadOnlyList<OrdreTransmis> Ordres => _ordres.ToArray();

    public Task TransmettreAsync(Entrepot entrepot, OrdrePreparationEntrepot ordre, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entrepot);
        ArgumentNullException.ThrowIfNull(ordre);
        cancellationToken.ThrowIfCancellationRequested();

        _ordres.Enqueue(new OrdreTransmis(entrepot, ordre, horloge.GetUtcNow()));
        JournaliserOrdre(ordre.NumeroOrdre, ordre.Site, ordre.Lignes.Count, ordre.Priorite, ordre.TypeFlux);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ordre {NumeroOrdre} transmis à {Site} : {NombreLignes} ligne(s), priorité {Priorite}, flux {TypeFlux}")]
    private partial void JournaliserOrdre(string numeroOrdre, string site, int nombreLignes, string priorite, string typeFlux);
}
