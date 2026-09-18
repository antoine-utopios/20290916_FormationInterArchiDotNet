using Microsoft.Extensions.DependencyInjection;
using Textinord.Extranet.Components.Pages;
using Textinord.Extranet.Modeles;
using Textinord.Extranet.Services;

namespace Textinord.Extranet.Tests.Pages;

public class CataloguePageTests : BunitContext
{
    private readonly PanierService panier = new(Client.Demonstration);

    public CataloguePageTests()
    {
        Services.AddSingleton<ICatalogueService>(new CatalogueEnMemoire(CatalogueDeTest.TrenteArticles()));
        Services.AddScoped(_ => panier);
    }

    [Fact]
    public void Affiche_la_premiere_page_de_douze_articles()
    {
        var cut = Render<Catalogue>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(12, cut.FindAll(".article-carte").Count);
            Assert.Contains("30 article(s), page 1 sur 3", cut.Find(".resume-resultats").TextContent);
        });
    }

    [Fact]
    public void Le_filtre_texte_reduit_la_liste()
    {
        var cut = Render<Catalogue>();
        cut.WaitForAssertion(() => Assert.Equal(12, cut.FindAll(".article-carte").Count));

        cut.Find("#texte").Change("gant");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(3, cut.FindAll(".article-carte").Count);
            Assert.Contains("3 article(s), page 1 sur 1", cut.Find(".resume-resultats").TextContent);
        });
    }

    [Fact]
    public void Le_filtre_en_stock_exclut_les_ruptures()
    {
        var cut = Render<Catalogue>();
        cut.WaitForAssertion(() => Assert.Equal(12, cut.FindAll(".article-carte").Count));

        cut.Find("#texte").Change("gant");
        cut.Find("#stock").Change(true);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".article-carte").Count));
    }

    [Fact]
    public void Ajouter_depuis_une_carte_alimente_le_panier()
    {
        var cut = Render<Catalogue>();
        cut.WaitForAssertion(() => Assert.Equal(12, cut.FindAll(".article-carte").Count));

        cut.Find(".article-carte input[type=number]").Change("4");
        cut.Find(".article-carte button").Click();

        Assert.Equal(4, panier.NombrePieces);
        cut.WaitForAssertion(() => Assert.Contains("4 x", cut.Find(".alert-success").TextContent));
    }

    [Fact]
    public void La_pagination_charge_la_page_demandee()
    {
        var cut = Render<Catalogue>();
        cut.WaitForAssertion(() => Assert.Equal(12, cut.FindAll(".article-carte").Count));

        cut.FindAll(".pagination button")[^1].Click();

        cut.WaitForAssertion(() => Assert.Contains("page 2 sur 3", cut.Find(".resume-resultats").TextContent));
    }
}
