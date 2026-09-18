using Microsoft.Extensions.Diagnostics.HealthChecks;
using Textinord.Trame.Api.Referentiel;

namespace Textinord.Trame.Api.Infrastructure;

/// <summary>
/// Sonde de disponibilité : l'API est « prête » si le référentiel articles est chargé.
/// Avec EF Core (module 4), on remplacera cette classe par `AddDbContextCheck` ou `AddSqlServer`.
/// </summary>
public sealed class StockHealthCheck(IReferentielArticles articles) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var nombre = articles.Tous().Count;
        var resultat = nombre > 0
            ? HealthCheckResult.Healthy($"{nombre} références chargées")
            : HealthCheckResult.Unhealthy("Référentiel articles vide");

        return Task.FromResult(resultat);
    }
}
