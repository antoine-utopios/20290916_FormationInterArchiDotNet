namespace Textinord.Trame.Validation;

/// <summary>
/// Validation d'une commande Textinord :
///   1. le client existe et est actif, sinon refus ;
///   2. au moins une ligne, quantités strictement positives, sinon refus ;
///   3. chaque ligne doit avoir un entrepôt avec du stock ; sinon la commande passe
///      en attente de stock et l'ADV est alertée pour chaque rupture ;
///   4. la remise (client + volume, plafond 30 %) est appliquée ligne par ligne ;
///   5. la date de validation vient de l'horloge injectée (testable).
/// </summary>
public sealed class ValidationCommandeService(
    IReferentielClients clients,
    IStockDisponible stock,
    INotificateurAdv notificateur,
    TimeProvider horloge)
{
    public async Task<ResultatValidation> ValiderAsync(Commande commande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(commande);

        var client = await clients.TrouverAsync(commande.CodeClient, ct);
        if (client is null)
        {
            return Refuser(commande, $"Client inconnu : {commande.CodeClient}.");
        }

        if (!client.Actif)
        {
            return Refuser(commande, $"Client inactif : {commande.CodeClient}.");
        }

        if (commande.Lignes.Count == 0)
        {
            return Refuser(commande, "Une commande doit contenir au moins une ligne.");
        }

        var quantitesInvalides = commande.Lignes
            .Where(l => l.Quantite <= 0)
            .Select(l => $"Quantité invalide sur {l.Reference} : {l.Quantite}.")
            .ToArray();
        if (quantitesInvalides.Length > 0)
        {
            return Refuser(commande, quantitesInvalides);
        }

        var ruptures = new List<string>();
        foreach (var ligne in commande.Lignes)
        {
            var entrepots = await stock.EntrepotsDisponiblesAsync(ligne.Reference, ligne.Quantite, ct);
            if (entrepots.Count == 0)
            {
                ruptures.Add(ligne.Reference);
                await notificateur.SignalerRuptureAsync(commande.Numero, ligne.Reference, ct);
                continue;
            }

            ligne.Entrepot = entrepots[0];
        }

        if (ruptures.Count > 0)
        {
            commande.Statut = StatutCommande.EnAttenteStock;
            return new ResultatValidation(
                StatutCommande.EnAttenteStock,
                ruptures.Select(r => $"Rupture de stock : {r}.").ToList());
        }

        foreach (var ligne in commande.Lignes)
        {
            ligne.TauxRemise = RegleRemise.Calculer(client.TauxRemise, ligne.Quantite);
        }

        commande.Statut = StatutCommande.Validee;
        commande.ValideeLe = horloge.GetUtcNow();
        return new ResultatValidation(StatutCommande.Validee, []);
    }

    private static ResultatValidation Refuser(Commande commande, params string[] motifs)
    {
        commande.Statut = StatutCommande.Refusee;
        return new ResultatValidation(StatutCommande.Refusee, motifs);
    }
}
