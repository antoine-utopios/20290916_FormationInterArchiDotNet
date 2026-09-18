using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Textinord.Trame.Api.Domaine;
using Textinord.Trame.Api.Infrastructure;

namespace Textinord.Trame.Api.Commandes;

/// <summary>
/// Référentiel public (statuts et transitions) : anonyme, mis en cache de sortie 5 minutes.
/// L'extranet et l'application scanner l'utilisent pour afficher les libellés sans les coder en dur.
/// </summary>
public static class ReferentielEndpoints
{
    private static readonly IReadOnlyList<StatutDescription> Statuts = Enum.GetValues<StatutCommande>()
        .Select(s => new StatutDescription(s, Libelle(s), Transitions(s)))
        .ToList();

    public static IEndpointRouteBuilder MapReferentiel(this IEndpointRouteBuilder app, ApiVersionSet versions)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(versions);

        app.MapGroup("/api/v{version:apiVersion}/referentiel")
            .WithApiVersionSet(versions)
            .WithTags("Référentiel")
            .AllowAnonymous()
            .MapGet("/statuts", () => TypedResults.Ok(Statuts))
            .CacheOutput(CacheTrame.PolitiqueReferentiel)
            .WithName("ListerStatuts")
            .WithSummary("Statuts d'une commande et transitions autorisées");

        return app;
    }

    private static string Libelle(StatutCommande statut) => statut switch
    {
        StatutCommande.Brouillon => "Brouillon",
        StatutCommande.EnAttenteStock => "En attente de stock",
        StatutCommande.Validee => "Validée",
        StatutCommande.EnPreparation => "En préparation",
        StatutCommande.Expediee => "Expédiée",
        StatutCommande.Facturee => "Facturée",
        StatutCommande.Annulee => "Annulée",
        _ => statut.ToString(),
    };

    private static IReadOnlyList<StatutCommande> Transitions(StatutCommande depuis) => Commande.TransitionsDepuis(depuis);
}
