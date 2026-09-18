namespace Textinord.Trame.Api.Commandes;

/// <summary>Un validateur retourne un dictionnaire champ → messages, vide si tout va bien.</summary>
public interface IValidateur<in T>
{
    IDictionary<string, string[]> Valider(T valeur);
}

/// <summary>
/// Règles de forme de la requête de création. Les règles métier (client existant, stock)
/// restent dans le domaine et le service : ici on vérifie seulement que la requête est bien formée.
/// </summary>
public sealed class CreerCommandeValidateur : IValidateur<CreerCommandeRequete>
{
    public const int MaxLignes = 200;

    public IDictionary<string, string[]> Valider(CreerCommandeRequete valeur)
    {
        ArgumentNullException.ThrowIfNull(valeur);

        var erreurs = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(valeur.CodeClient))
        {
            Ajouter(erreurs, "codeClient", "Le code client est obligatoire.");
        }

        if (valeur.Lignes is null || valeur.Lignes.Count == 0)
        {
            Ajouter(erreurs, "lignes", "Une commande contient au moins une ligne.");
        }
        else
        {
            if (valeur.Lignes.Count > MaxLignes)
            {
                Ajouter(erreurs, "lignes", $"Une commande contient au plus {MaxLignes} lignes.");
            }

            for (var i = 0; i < valeur.Lignes.Count; i++)
            {
                var ligne = valeur.Lignes[i];
                if (string.IsNullOrWhiteSpace(ligne.Reference))
                {
                    Ajouter(erreurs, $"lignes[{i}].reference", "La référence est obligatoire.");
                }

                if (ligne.Quantite <= 0)
                {
                    Ajouter(erreurs, $"lignes[{i}].quantite", "La quantité est strictement positive.");
                }

                if (ligne.PrixNegocie is <= 0m)
                {
                    Ajouter(erreurs, $"lignes[{i}].prixNegocie", "Le prix négocié est strictement positif.");
                }
            }
        }

        return erreurs.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray(), StringComparer.Ordinal);
    }

    private static void Ajouter(Dictionary<string, List<string>> erreurs, string champ, string message)
    {
        if (!erreurs.TryGetValue(champ, out var liste))
        {
            liste = [];
            erreurs[champ] = liste;
        }

        liste.Add(message);
    }
}

/// <summary>
/// Endpoint filter générique : valide l'argument de type T avant d'appeler le handler,
/// et répond 400 ValidationProblem sinon. Construit une fois par endpoint à partir des
/// services de l'application : ses dépendances doivent donc être des singletons.
/// </summary>
public sealed class ValidationFilter<T>(IValidateur<T> validateur) : IEndpointFilter
    where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Corps de requête manquant ou illisible",
                detail: $"Un objet {typeof(T).Name} est attendu au format JSON.");
        }

        var erreurs = validateur.Valider(argument);
        if (erreurs.Count > 0)
        {
            return TypedResults.ValidationProblem(erreurs, title: "La requête est invalide");
        }

        return await next(context);
    }
}
