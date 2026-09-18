using System.Diagnostics;
using Textinord.Trame.Api.Domaine;
using Textinord.Trame.Api.Infrastructure;
using Textinord.Trame.Api.Referentiel;

namespace Textinord.Trame.Api.Commandes;

/// <summary>Résultat d'une création : soit une commande, soit une erreur métier typée.</summary>
public sealed record ResultatCreation(Commande? Commande, string? CodeErreur, string? Valeur)
{
    public static ResultatCreation Succes(Commande commande) => new(commande, null, null);

    public static ResultatCreation ClientInconnu(string code) => new(null, "CLIENT_INCONNU", code);

    public static ResultatCreation ArticleInconnu(string reference) => new(null, "ARTICLE_INCONNU", reference);
}

/// <summary>
/// Service applicatif des commandes, enregistré en Scoped : une instance par requête HTTP.
/// Il orchestre le domaine, les référentiels (singletons) et la télémétrie ; il ne contient
/// aucune règle métier (elles sont dans <see cref="Commande"/>).
/// </summary>
public sealed class CommandesService(
    ICommandesStore store,
    IGenerateurNumero numeros,
    IReferentielClients clients,
    IReferentielArticles articles,
    TimeProvider horloge,
    ILogger<CommandesService> logger)
{
    public Page<CommandeResume> Rechercher(StatutCommande? statut, string? codeClient, int page, int taille)
    {
        var elements = store.Rechercher(statut, codeClient, page, taille, out var total);
        return new Page<CommandeResume>(elements.Select(c => c.VersResume()).ToList(), page, taille, total);
    }

    public Commande? Trouver(string numero) => store.Trouver(numero);

    public ResultatCreation Creer(CreerCommandeRequete requete)
    {
        ArgumentNullException.ThrowIfNull(requete);

        using var activite = Telemetrie.Source.StartActivity("commande.creer");

        var client = clients.Trouver(requete.CodeClient);
        if (client is null)
        {
            return ResultatCreation.ClientInconnu(requete.CodeClient);
        }

        var lignes = new List<(Article Article, LigneRequete Demande)>();
        foreach (var ligne in requete.Lignes)
        {
            var article = articles.Trouver(ligne.Reference);
            if (article is null)
            {
                return ResultatCreation.ArticleInconnu(ligne.Reference);
            }

            lignes.Add((article, ligne));
        }

        var aujourdHui = DateOnly.FromDateTime(horloge.GetLocalNow().DateTime);
        var commande = new Commande(numeros.Suivant(aujourdHui), client.Code, client.TauxRemise, aujourdHui);

        foreach (var (article, demande) in lignes)
        {
            commande.AjouterLigne(article.Reference, demande.Quantite, demande.PrixNegocie ?? article.PrixBase);
        }

        store.Ajouter(commande);

        activite?.SetTag("commande.numero", commande.Numero);
        activite?.SetTag("commande.client", commande.CodeClient);
        Telemetrie.CommandesCreees.Add(1, new KeyValuePair<string, object?>("client.condition", client.ConditionTarifaire));
        logger.LogInformation("Commande {Numero} créée pour {Client} ({Lignes} lignes, {Total:0.00} € HT)",
            commande.Numero, commande.CodeClient, commande.Lignes.Count, commande.TotalHT);

        return ResultatCreation.Succes(commande);
    }

    public Result Valider(Commande commande)
    {
        ArgumentNullException.ThrowIfNull(commande);

        using var activite = Telemetrie.Source.StartActivity("commande.valider");
        activite?.SetTag("commande.numero", commande.Numero);

        var resultat = commande.Valider((reference, quantite) => articles.Trouver(reference)?.EntrepotPouvantServir(quantite));

        if (resultat.EstSucces)
        {
            if (commande.Statut == StatutCommande.Validee)
            {
                Telemetrie.CommandesValidees.Add(1);
                logger.LogInformation("Commande {Numero} validée, ordres de préparation à émettre", commande.Numero);
            }
            else
            {
                Telemetrie.CommandesEnAttenteStock.Add(1);
                logger.LogWarning("Commande {Numero} en attente de stock", commande.Numero);
            }
        }

        activite?.SetStatus(resultat.EstSucces ? ActivityStatusCode.Ok : ActivityStatusCode.Error, resultat.Erreur?.Code);
        return resultat;
    }

    public Result Annuler(Commande commande)
    {
        ArgumentNullException.ThrowIfNull(commande);

        var resultat = commande.Annuler();
        if (resultat.EstSucces)
        {
            logger.LogInformation("Commande {Numero} annulée", commande.Numero);
        }

        return resultat;
    }
}
