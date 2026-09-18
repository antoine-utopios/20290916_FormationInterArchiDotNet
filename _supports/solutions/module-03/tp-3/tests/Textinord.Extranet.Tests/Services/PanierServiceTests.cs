using Textinord.Extranet.Modeles;
using Textinord.Extranet.Services;

namespace Textinord.Extranet.Tests.Services;

public class PanierServiceTests
{
    private static PanierService Panier() => new(new Client("HOT-0421", "Hôtel Beaulieu Lille", 10m));

    [Fact]
    public void Ajouter_deux_fois_le_meme_article_fusionne_les_lignes()
    {
        var panier = Panier();
        var article = CatalogueDeTest.Article("EP-001", "Gants anti-coupure", 6.80m);

        panier.Ajouter(article, 100);
        panier.Ajouter(article, 450);

        var ligne = Assert.Single(panier.Lignes);
        Assert.Equal(550, ligne.Quantite);
        Assert.Equal(15m, ligne.TauxRemise);
        Assert.Equal(3740m, ligne.MontantBrut);
        Assert.Equal(561m, ligne.MontantRemise);
        Assert.Equal(3179m, panier.TotalNet);
    }

    [Fact]
    public void Modifier_la_quantite_a_zero_retire_la_ligne_et_notifie()
    {
        var panier = Panier();
        var notifications = 0;
        panier.Changement += () => notifications++;
        panier.Ajouter(CatalogueDeTest.Article("VT-001", "Veste"), 3);

        panier.ModifierQuantite("VT-001", 0);

        Assert.True(panier.EstVide);
        Assert.Equal(2, notifications);
    }

    [Fact]
    public void Une_quantite_invalide_est_refusee()
    {
        var panier = Panier();

        Assert.Throws<ArgumentOutOfRangeException>(() => panier.Ajouter(CatalogueDeTest.Article("VT-001", "Veste"), 0));
    }
}
