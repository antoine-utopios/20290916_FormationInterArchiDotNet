using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Textinord.Trame.Api.Infrastructure;

/// <summary>
/// Sources de télémétrie applicative : une ActivitySource pour les spans métier,
/// un Meter pour les compteurs. OpenTelemetry les exporte vers la console (local),
/// le dashboard Aspire (OTLP) ou Application Insights (production).
/// </summary>
public static class Telemetrie
{
    public const string NomService = "Textinord.Trame.Api";

    public static readonly ActivitySource Source = new(NomService);

    public static readonly Meter Meter = new(NomService);

    public static readonly Counter<long> CommandesCreees =
        Meter.CreateCounter<long>("trame.commandes.creees", unit: "{commande}", description: "Commandes créées via l'API");

    public static readonly Counter<long> CommandesValidees =
        Meter.CreateCounter<long>("trame.commandes.validees", unit: "{commande}", description: "Commandes validées (stock disponible)");

    public static readonly Counter<long> CommandesEnAttenteStock =
        Meter.CreateCounter<long>("trame.commandes.attente_stock", unit: "{commande}", description: "Commandes passées en attente de stock");
}
