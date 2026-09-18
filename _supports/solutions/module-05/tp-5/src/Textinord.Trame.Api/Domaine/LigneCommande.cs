namespace Textinord.Trame.Api.Domaine;

/// <summary>
/// Une ligne de commande : article, quantité, prix unitaire négocié et remise calculée.
/// Construite uniquement par <see cref="Commande.AjouterLigne"/> : l'agrégat garde la main sur ses invariants.
/// </summary>
public sealed class LigneCommande
{
    internal LigneCommande(int numero, string reference, int quantite, decimal prixUnitaire, Remise remise)
    {
        Numero = numero;
        Reference = reference;
        Quantite = quantite;
        PrixUnitaire = prixUnitaire;
        Remise = remise;
    }

    public int Numero { get; }

    public string Reference { get; }

    public int Quantite { get; }

    public decimal PrixUnitaire { get; }

    public Remise Remise { get; }

    /// <summary>Entrepôt qui servira la ligne, affecté à la validation ; null tant que la commande n'est pas validée.</summary>
    public string? EntrepotAffecte { get; internal set; }

    public decimal MontantBrut => PrixUnitaire * Quantite;

    public decimal MontantRemise => Math.Round(MontantBrut * Remise.TauxApplique, 2, MidpointRounding.ToEven);

    public decimal MontantNet => MontantBrut - MontantRemise;
}
