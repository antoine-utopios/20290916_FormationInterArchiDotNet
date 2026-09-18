using Microsoft.EntityFrameworkCore;
using Textinord.Trame.Domain;
using Textinord.Trame.Domain.Services;
using Textinord.Trame.Infrastructure;
using Textinord.Trame.Infrastructure.Outbox;

namespace Textinord.Trame.Api.Commandes;

/// <summary>
/// Cas d'usage « prendre une commande » : contrôle, numérotation, règle de stock, puis
/// écriture de la commande ET du message Outbox dans une seule transaction.
/// </summary>
public sealed class PriseDeCommande(TrameDbContext db, IStockDisponible stock, TimeProvider horloge)
{
    public async Task<Resultat<Commande>> EnregistrerAsync(NouvelleCommandeRequete requete, CancellationToken ct = default)
    {
        var erreurs = new List<string>();

        if (requete.Lignes is null || requete.Lignes.Count == 0)
        {
            erreurs.Add("Une commande doit contenir au moins une ligne.");
        }

        var client = string.IsNullOrWhiteSpace(requete.CodeClient)
            ? null
            : await db.Clients.FindAsync([requete.CodeClient], ct);

        if (client is null || !client.Actif)
        {
            erreurs.Add($"Client inconnu ou inactif : {requete.CodeClient}.");
        }

        if (erreurs.Count > 0)
        {
            return Resultat<Commande>.Echec([.. erreurs]);
        }

        var references = requete.Lignes!.Select(l => l.Reference).Distinct().ToList();
        var articles = await db.Articles
            .Where(a => references.Contains(a.Reference))
            .ToDictionaryAsync(a => a.Reference, ct);

        var lignes = new List<LigneCommande>();
        foreach (var ligne in requete.Lignes!)
        {
            if (!articles.TryGetValue(ligne.Reference, out var article))
            {
                erreurs.Add($"Article inconnu : {ligne.Reference}.");
                continue;
            }

            if (ligne.Quantite <= 0)
            {
                erreurs.Add($"Quantité invalide pour {ligne.Reference} : {ligne.Quantite}.");
                continue;
            }

            lignes.Add(new LigneCommande(article.Reference, ligne.Quantite, article.PrixBase, client!.TauxRemise));
        }

        if (erreurs.Count > 0)
        {
            return Resultat<Commande>.Echec([.. erreurs]);
        }

        var maintenant = horloge.GetUtcNow();
        var numero = await ProchainNumeroAsync(maintenant.Year, ct);

        var creation = Commande.Creer(numero, client!.Code, maintenant, lignes);
        if (!creation.EstSucces)
        {
            return creation;
        }

        var commande = creation.Valeur!;

        // Règle de stock : un entrepôt par référence, ou null en rupture.
        var affectations = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var ligne in commande.Lignes)
        {
            affectations[ligne.ReferenceArticle] =
                await stock.PremierEntrepotDisponibleAsync(ligne.ReferenceArticle, ligne.Quantite, ct);
        }

        var evenement = commande.Valider(affectations, maintenant);

        db.Commandes.Add(commande);
        if (evenement is not null)
        {
            db.Outbox.Add(OutboxSerialiseur.Emballer(evenement, maintenant));
        }

        // Un seul SaveChanges = une seule transaction : commande, lignes, compteur et message
        // Outbox sont écrits ensemble ou rejetés ensemble. Aucun DTC, aucune transaction distribuée.
        await db.SaveChangesAsync(ct);

        return Resultat<Commande>.Succes(commande);
    }

    private async Task<string> ProchainNumeroAsync(int annee, CancellationToken ct)
    {
        var compteur = await db.Compteurs.FindAsync([annee], ct);
        if (compteur is null)
        {
            compteur = new CompteurCommandes { Annee = annee, Dernier = 0 };
            db.Compteurs.Add(compteur);
        }

        compteur.Dernier++;
        return NumeroCommande.Formater(annee, compteur.Dernier);
    }
}
