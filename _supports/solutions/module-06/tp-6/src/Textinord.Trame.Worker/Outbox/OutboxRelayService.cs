using Microsoft.Extensions.Options;

namespace Textinord.Trame.Worker.Outbox;

/// <summary>Boucle de fond : un cycle de relais toutes les <see cref="OutboxOptions.Intervalle"/>.</summary>
public sealed class OutboxRelayService(
    IServiceScopeFactory scopes,
    IOptions<OutboxOptions> options,
    ILogger<OutboxRelayService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reglages = options.Value;
        logger.LogInformation("Relais Outbox démarré : toutes les {Intervalle}, lots de {Lot}",
            reglages.Intervalle, reglages.TailleLot);

        using var minuteur = new PeriodicTimer(reglages.Intervalle);

        try
        {
            do
            {
                await RelayerUnCycleAsync(reglages, stoppingToken);
            }
            while (await minuteur.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Relais Outbox arrêté");
        }
    }

    private async Task RelayerUnCycleAsync(OutboxOptions reglages, CancellationToken ct)
    {
        try
        {
            // Le DbContext est scoped : un scope par cycle, jamais un DbContext partagé par le singleton.
            await using var scope = scopes.CreateAsyncScope();
            var relais = scope.ServiceProvider.GetRequiredService<OutboxRelay>();
            var envoyes = await relais.RelayerAsync(reglages.TailleLot, reglages.TentativesMaximum, ct);

            if (envoyes > 0)
            {
                logger.LogInformation("{Nombre} message(s) Outbox publié(s)", envoyes);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Une base indisponible ne tue pas le Worker : on réessaie au prochain cycle.
            logger.LogError(ex, "Cycle de relais Outbox en échec, nouvelle tentative au prochain cycle");
        }
    }
}
