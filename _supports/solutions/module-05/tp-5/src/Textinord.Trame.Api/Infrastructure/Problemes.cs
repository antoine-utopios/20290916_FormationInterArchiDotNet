using Microsoft.AspNetCore.Http.HttpResults;
using Textinord.Trame.Api.Domaine;

namespace Textinord.Trame.Api.Infrastructure;

/// <summary>
/// Fabrique de ProblemDetails (RFC 9457) : une seule forme d'erreur pour toute l'API.
/// Le champ « type » est une URI stable que les clients peuvent tester ; « code » reprend
/// le code métier du pattern Result, pour ne pas obliger le client à parser le message.
/// </summary>
public static class Problemes
{
    public const string BaseType = "https://trame.textinord.example/problemes/";

    public static ProblemHttpResult CommandeIntrouvable(string numero) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            type: BaseType + "commande-introuvable",
            title: "Commande introuvable",
            detail: $"Aucune commande ne porte le numéro {numero}.",
            extensions: new Dictionary<string, object?> { ["numero"] = numero });

    public static ProblemHttpResult ClientInconnu(string codeClient) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status422UnprocessableEntity,
            type: BaseType + "client-inconnu",
            title: "Client inconnu",
            detail: $"Le code client {codeClient} n'existe pas dans le référentiel.",
            extensions: new Dictionary<string, object?> { ["codeClient"] = codeClient });

    public static ProblemHttpResult ArticleInconnu(string reference) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status422UnprocessableEntity,
            type: BaseType + "article-inconnu",
            title: "Article inconnu",
            detail: $"La référence {reference} n'existe pas au catalogue.",
            extensions: new Dictionary<string, object?> { ["reference"] = reference });

    /// <summary>Un échec métier (transition interdite, commande vide) devient un 409 Conflict avec son code.</summary>
    public static ProblemHttpResult ConflitMetier(Erreur erreur, string numero) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: BaseType + erreur.Code.ToLowerInvariant().Replace('_', '-'),
            title: "Opération impossible dans l'état actuel de la commande",
            detail: erreur.Message,
            extensions: new Dictionary<string, object?> { ["code"] = erreur.Code, ["numero"] = numero });
}
