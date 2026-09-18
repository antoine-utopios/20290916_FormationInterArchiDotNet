using MassTransit;
using Microsoft.EntityFrameworkCore;
using Textinord.Trame.Infrastructure;
using Textinord.Trame.Infrastructure.Outbox;

namespace Textinord.Trame.Worker.Outbox;

/// <summary>
/// Relais Outbox : lit les messages non envoyés, les publie sur le bus, les marque envoyés.
/// Garantie « au moins une fois » : si le processus tombe entre la publication et le marquage,
/// le message sera republié ; c'est le consumer qui est idempotent.
/// </summary>
public sealed class OutboxRelay(
    TrameDbContext db,
    IPublishEndpoint publication,
    TimeProvider horloge,
    ILogger<OutboxRelay> logger)
{
    public async Task<int> RelayerAsync(int tailleLot, int tentativesMaximum, CancellationToken ct)
    {
        var lot = await db.Outbox
            .Where(m => m.EnvoyeLe == null && m.Tentatives < tentativesMaximum)
            .OrderBy(m => m.Id)
            .Take(tailleLot)
            .ToListAsync(ct);

        var envoyes = 0;
        foreach (var message in lot)
        {
            try
            {
                var (contenu, type) = OutboxSerialiseur.Deballer(message);

                // Le MessageId du bus est celui de la ligne Outbox : un rejeu produit le même identifiant.
                await publication.Publish(
                    contenu,
                    type,
                    Pipe.Execute<PublishContext>(ctx => ctx.MessageId = message.MessageId),
                    ct);

                message.EnvoyeLe = horloge.GetUtcNow();
                envoyes++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.Tentatives++;
                message.DerniereErreur = ex.Message;
                logger.LogWarning(ex, "Publication du message Outbox {Id} ({Type}) échouée, tentative {Tentatives}",
                    message.Id, message.Type, message.Tentatives);
            }
        }

        await db.SaveChangesAsync(ct);
        return envoyes;
    }
}
