namespace Textinord.Trame.Domain;

/// <summary>Ligne d'une commande : article, quantité, prix négocié et remise appliquée.</summary>
public sealed class LigneCommande
{
    public int Id { get; private set; }
    public int ArticleId { get; private set; }
    public string ReferenceArticle { get; private set; }
    public int Quantite { get; private set; }
    public decimal PrixUnitaire { get; private set; }
    public decimal RemisePourcent { get; private set; }

    private LigneCommande()
    {
        ReferenceArticle = string.Empty;
    }

    internal LigneCommande(Article article, int quantite, decimal prixUnitaire, decimal remisePourcent)
    {
        ArgumentNullException.ThrowIfNull(article);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantite);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(prixUnitaire);
        ArgumentOutOfRangeException.ThrowIfNegative(remisePourcent);
        ArticleId = article.Id;
        ReferenceArticle = article.Reference;
        Quantite = quantite;
        PrixUnitaire = prixUnitaire;
        RemisePourcent = remisePourcent;
    }

    public decimal MontantHt => Math.Round(Quantite * PrixUnitaire * (1 - RemisePourcent / 100m), 2);
}
