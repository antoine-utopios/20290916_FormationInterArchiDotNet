using Textinord.Trame.Domain.Commandes.Specifications;
using Textinord.Trame.Domain.Communs;
using Textinord.Trame.Domain.Tests.Fixtures;

namespace Textinord.Trame.Domain.Tests.Communs;

public class SpecificationEtResultTests
{
    [Fact]
    public void La_specification_de_stock_isole_les_lignes_bloquantes()
    {
        var commande = JeuDeDonnees.NouvelleCommande(JeuDeDonnees.HotelDuBeffroi());
        commande.AjouterLigne(JeuDeDonnees.VesteDeTravail(), quantite: 10);
        commande.AjouterLigne(JeuDeDonnees.DrapHotellerie(), quantite: 50);
        var regle = new CommandeValidableSpecification();

        var bloquantes = regle.LignesSansStock(commande);

        Assert.False(regle.EstSatisfaitePar(commande));
        Assert.Single(bloquantes);
        Assert.Equal("LH-0901", bloquantes[0].Article.Reference);
    }

    [Fact]
    public void Les_specifications_se_composent_avec_Et_Ou_Non()
    {
        var pair = Specification<int>.Depuis(n => n % 2 == 0);
        var positif = Specification<int>.Depuis(n => n > 0);

        Assert.True(pair.Et(positif).EstSatisfaitePar(4));
        Assert.False(pair.Et(positif).EstSatisfaitePar(-4));
        Assert.True(pair.Ou(positif).EstSatisfaitePar(3));
        Assert.True(pair.Non().EstSatisfaitePar(3));
    }

    [Fact]
    public void Un_result_en_echec_porte_son_erreur_et_refuse_de_livrer_une_valeur()
    {
        var echec = Result<int>.Echec("STOCK_INSUFFISANT", "Rupture sur LH-0901.");
        var succes = Result.Ok(42);

        Assert.True(echec.EstEchec);
        Assert.Equal("STOCK_INSUFFISANT", echec.Erreur!.Code);
        Assert.Throws<InvalidOperationException>(() => echec.Valeur);
        Assert.Equal(42, succes.Valeur);
        Assert.Equal("ko", echec.Selon(_ => "ok", _ => "ko"));
    }
}
