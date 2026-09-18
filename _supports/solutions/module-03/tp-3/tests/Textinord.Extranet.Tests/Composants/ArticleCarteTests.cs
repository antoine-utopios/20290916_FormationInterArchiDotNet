using Textinord.Extranet.Components.Partages;
using Textinord.Extranet.Modeles;

namespace Textinord.Extranet.Tests.Composants;

public class ArticleCarteTests : BunitContext
{
    [Fact]
    public void Affiche_le_libelle_la_reference_le_prix_et_le_stock()
    {
        var article = CatalogueDeTest.Article("EP-001", "Gants anti-coupure niveau C", 6.80m, 100, 50);

        var cut = Render<ArticleCarte>(p => p.Add(c => c.Article, article));

        Assert.Contains("Gants anti-coupure niveau C", cut.Find(".card-title").TextContent);
        Assert.Contains("EP-001", cut.Find(".card-subtitle").TextContent);
        Assert.Contains("6,80 €", cut.Find(".prix").TextContent);
        Assert.Contains("150 en stock", cut.Find(".stock").TextContent);
        Assert.False(cut.Find("button").HasAttribute("disabled"));
    }

    [Fact]
    public void Un_article_en_rupture_desactive_le_bouton()
    {
        var article = CatalogueDeTest.Article("EP-003", "Gants soudeur cuir", 9.40m, 0, 0);

        var cut = Render<ArticleCarte>(p => p.Add(c => c.Article, article));

        Assert.Contains("Rupture", cut.Find(".stock").TextContent);
        Assert.True(cut.Find("button").HasAttribute("disabled"));
    }

    [Fact]
    public void Ajouter_remonte_l_article_et_la_quantite_saisie_au_parent()
    {
        var article = CatalogueDeTest.Article("EP-001", "Gants anti-coupure niveau C", 6.80m);
        AjoutPanier? recu = null;

        var cut = Render<ArticleCarte>(p => p
            .Add(c => c.Article, article)
            .Add(c => c.OnAjouter, (AjoutPanier ajout) => recu = ajout));

        cut.Find("input[type=number]").Change("25");
        cut.Find("button").Click();

        Assert.NotNull(recu);
        Assert.Equal("EP-001", recu.Article.Reference);
        Assert.Equal(25, recu.Quantite);
        Assert.Equal("1", cut.Find("input[type=number]").GetAttribute("value"));
    }
}
