using MassTransit;
using Microsoft.EntityFrameworkCore;
using Textinord.Trame.Domain;
using Textinord.Trame.Domain.Events;
using Textinord.Trame.Infrastructure;

namespace Textinord.Trame.Worker.Consumers;

/// <summary>
/// À chaque commande validée, un ordre de préparation par entrepôt concerné.
/// Idempotent : le même message rejoué (retry, relais Outbox interrompu, redélivrance du broker)
/// ne crée jamais un second ordre pour le même couple (commande, entrepôt).
/// </summary>
public sealed class OrdrePreparationConsumer(
    TrameDbContext db,
    TimeProvider horloge,
    ILogger<OrdrePreparationConsumer> logger) : IConsumer<CommandeValidee>
{
    public async Task Consume(ConsumeContext<CommandeValidee> context)
    {
        var message = context.Message;
        var ct = context.CancellationToken;

        var dejaEmis = await db.OrdresPreparation
            .Where(o => o.CommandeId == message.CommandeId)
            .Select(o => o.Entrepot)
            .ToListAsync(ct);

        var nouveaux = message.Lignes
            .GroupBy(l => l.Entrepot, StringComparer.Ordinal)
            .Where(g => !dejaEmis.Contains(g.Key, StringComparer.Ordinal))
            .Select(g => OrdrePreparation.Depuis(message, g, horloge.GetUtcNow()))
            .ToList();

        if (nouveaux.Count == 0)
        {
            logger.LogInformation("Message {MessageId} rejoué : ordres de {Numero} déjà émis, rien à faire",
                context.MessageId, message.Numero);
            return;
        }

        db.OrdresPreparation.AddRange(nouveaux);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Deux instances ont traité le même message en parallèle : l'index unique tranche.
            logger.LogInformation("Ordres de {Numero} créés en parallèle par une autre instance, message {MessageId} ignoré",
                message.Numero, context.MessageId);
            return;
        }

        foreach (var ordre in nouveaux)
        {
            logger.LogInformation("Ordre de préparation {Entrepot} émis pour {Numero} : {Lignes} ligne(s), {Pieces} pièce(s)",
                ordre.Entrepot, ordre.NumeroCommande, ordre.NombreLignes, ordre.NombrePieces);
        }
    }
}
