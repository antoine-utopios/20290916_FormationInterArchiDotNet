namespace Textinord.Extranet.Modeles;

/// <summary>Ligne du panier : un article, une quantité, la remise calculée.</summary>
public sealed class LignePanier
{
    private readonly decimal remiseClientPourcent;

    public LignePanier(Article article, int quantite, decimal remiseClientPourcent)
    {
        Article = article;
        Quantite = quantite;
        this.remiseClientPourcent = remiseClientPourcent;
    }

    public Article Article { get; }

    public int Quantite { get; set; }

    public decimal PrixUnitaire => Article.PrixBase;

    public decimal TauxRemise => Tarification.CalculerTauxRemise(remiseClientPourcent, Quantite);

    public decimal MontantBrut => PrixUnitaire * Quantite;

    public decimal MontantRemise => Math.Round(MontantBrut * TauxRemise / 100m, 2);

    public decimal MontantNet => MontantBrut - MontantRemise;
}

/// <summary>Demande d'ajout au panier émise par une carte article.</summary>
public sealed record AjoutPanier(Article Article, int Quantite);
