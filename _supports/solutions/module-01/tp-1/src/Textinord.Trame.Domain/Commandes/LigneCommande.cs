using Textinord.Trame.Domain.Catalogue;
using Textinord.Trame.Domain.Tarification;

namespace Textinord.Trame.Domain.Commandes;

/// <summary>
/// Une ligne de commande : article, quantité, prix unitaire négocié et remise calculée.
/// Construite uniquement par <see cref="Commande.AjouterLigne"/> : l'agrégat garde la main sur ses invariants.
/// </summary>
public sealed class LigneCommande
{
    internal LigneCommande(int numero, Article article, int quantite, decimal prixUnitaire, Remise remise)
    {
        Numero = numero;
        Article = article;
        Quantite = quantite;
        PrixUnitaire = prixUnitaire;
        Remise = remise;
    }

    public int Numero { get; }

    public Article Article { get; }

    public int Quantite { get; }

    /// <summary>Prix unitaire négocié pour cette commande (par défaut, le prix de base du catalogue).</summary>
    public decimal PrixUnitaire { get; }

    public Remise Remise { get; }

    /// <summary>Entrepôt qui servira la ligne, affecté à la validation ; null tant que la commande n'est pas validée.</summary>
    public Entrepot? EntrepotAffecte { get; internal set; }

    public decimal MontantBrut => PrixUnitaire * Quantite;

    public decimal MontantNet => Math.Round(MontantBrut * (1 - Remise.TauxApplique), 2, MidpointRounding.ToEven);

    public override string ToString() => $"{Numero}. {Article.Reference} x {Quantite} @ {PrixUnitaire:0.00} (-{Remise.TauxApplique:P0})";
}
