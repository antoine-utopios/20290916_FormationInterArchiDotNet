using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Textinord.Trame.Api.Domaine;
using Textinord.Trame.Api.Infrastructure;
using Textinord.Trame.Api.Securite;

namespace Textinord.Trame.Api.Commandes;

/// <summary>
/// Les endpoints REST des commandes, regroupés par ressource et versionnés par segment d'URL.
/// Chaque handler retourne des TypedResults : le contrat (codes, schémas) est lu par OpenAPI
/// sans attribut supplémentaire, et le compilateur vérifie qu'on ne renvoie rien d'imprévu.
/// </summary>
public static class CommandesEndpoints
{
    public const int TaillePageMax = 100;

    public static IEndpointRouteBuilder MapCommandes(this IEndpointRouteBuilder app, ApiVersionSet versions)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(versions);

        var commandes = app.MapGroup("/api/v{version:apiVersion}/commandes")
            .WithApiVersionSet(versions)
            .WithTags("Commandes")
            .RequireAuthorization(Politiques.LectureCommandes)
            .RequireRateLimiting(RateLimitingTrame.PolitiqueApi)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // Lecture — disponible en v1 et v2 (représentation identique).
        commandes.MapGet("/", Rechercher)
            .WithName("RechercherCommandes")
            .WithSummary("Liste paginée des commandes")
            .WithDescription("Filtre facultatif par statut et par code client. La taille de page est plafonnée à 100.");

        commandes.MapGet("/{numero}", ObtenirV1)
            .MapToApiVersion(1.0)
            .WithName("ObtenirCommandeV1")
            .WithSummary("Détail d'une commande (v1 : taux de remise unique par ligne)");

        commandes.MapGet("/{numero}", ObtenirV2)
            .MapToApiVersion(2.0)
            .WithName("ObtenirCommandeV2")
            .WithSummary("Détail d'une commande (v2 : remise expliquée et totaux décomposés)");

        // Écriture — réservée à l'ADV ; la validation de forme passe par un endpoint filter.
        commandes.MapPost("/", Creer)
            .RequireAuthorization(Politiques.EcritureCommandes)
            .AddEndpointFilter<ValidationFilter<CreerCommandeRequete>>()
            .WithName("CreerCommande")
            .WithSummary("Crée une commande en brouillon")
            .WithDescription("Le numéro CMD-AAAA-NNNNNN est attribué par le serveur. Les remises client et volume sont calculées ligne par ligne, plafonnées à 30 %.");

        commandes.MapPost("/{numero}/validation", Valider)
            .RequireAuthorization(Politiques.EcritureCommandes)
            .WithName("ValiderCommande")
            .WithSummary("Valide une commande")
            .WithDescription("Passe la commande en Validee si chaque ligne a du stock dans un entrepôt, sinon en EnAttenteStock. Idempotent tant que la commande est déjà validée : un second appel répond 409.");

        commandes.MapDelete("/{numero}", Annuler)
            .RequireAuthorization(Politiques.EcritureCommandes)
            .WithName("AnnulerCommande")
            .WithSummary("Annule une commande (possible avant préparation)");

        return app;
    }

    private static Ok<Page<CommandeResume>> Rechercher(
        CommandesService service,
        StatutCommande? statut,
        string? client,
        int page = 1,
        int taille = 20)
    {
        page = Math.Max(page, 1);
        taille = Math.Clamp(taille, 1, TaillePageMax);

        return TypedResults.Ok(service.Rechercher(statut, client, page, taille));
    }

    private static Results<Ok<CommandeDetailV1>, ProblemHttpResult> ObtenirV1(string numero, CommandesService service) =>
        service.Trouver(numero) is { } commande
            ? TypedResults.Ok(commande.VersDetailV1())
            : Problemes.CommandeIntrouvable(numero);

    private static Results<Ok<CommandeDetailV2>, ProblemHttpResult> ObtenirV2(string numero, CommandesService service) =>
        service.Trouver(numero) is { } commande
            ? TypedResults.Ok(commande.VersDetailV2())
            : Problemes.CommandeIntrouvable(numero);

    private static Results<Created<CommandeDetailV1>, ValidationProblem, ProblemHttpResult> Creer(
        CreerCommandeRequete requete,
        CommandesService service,
        HttpContext http)
    {
        var resultat = service.Creer(requete);

        if (resultat.Commande is null)
        {
            return resultat.CodeErreur switch
            {
                "CLIENT_INCONNU" => Problemes.ClientInconnu(resultat.Valeur!),
                _ => Problemes.ArticleInconnu(resultat.Valeur!),
            };
        }

        // L'URL de la ressource créée respecte la version demandée par le client (v1 ou v2).
        // RequestedApiVersion est une propriété d'extension (C# 14) fournie par Asp.Versioning.Http.
        var version = http.RequestedApiVersion?.MajorVersion ?? 1;
        var emplacement = $"/api/v{version}/commandes/{resultat.Commande.Numero}";

        return TypedResults.Created(emplacement, resultat.Commande.VersDetailV1());
    }

    private static Results<Ok<CommandeDetailV1>, ProblemHttpResult> Valider(string numero, CommandesService service)
    {
        var commande = service.Trouver(numero);
        if (commande is null)
        {
            return Problemes.CommandeIntrouvable(numero);
        }

        var resultat = service.Valider(commande);

        return resultat.EstSucces
            ? TypedResults.Ok(commande.VersDetailV1())
            : Problemes.ConflitMetier(resultat.Erreur!, numero);
    }

    private static Results<NoContent, ProblemHttpResult> Annuler(string numero, CommandesService service)
    {
        var commande = service.Trouver(numero);
        if (commande is null)
        {
            return Problemes.CommandeIntrouvable(numero);
        }

        var resultat = service.Annuler(commande);

        return resultat.EstSucces
            ? TypedResults.NoContent()
            : Problemes.ConflitMetier(resultat.Erreur!, numero);
    }
}
