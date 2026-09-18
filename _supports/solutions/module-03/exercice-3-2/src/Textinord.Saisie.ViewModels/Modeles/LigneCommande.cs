namespace Textinord.Saisie.ViewModels.Modeles;

public sealed record LigneCommande(Article Article, int Quantite, decimal TauxRemise)
{
    public decimal MontantBrut => Article.PrixBase * Quantite;

    public decimal MontantNet => Math.Round(MontantBrut * (1 - TauxRemise / 100m), 2);
}
