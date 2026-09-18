using Textinord.Extranet.Components.Partages;

namespace Textinord.Extranet.Tests.Composants;

public class PaginationTests : BunitContext
{
    [Fact]
    public void Sur_la_premiere_page_le_bouton_precedent_est_desactive()
    {
        var cut = Render<Pagination>(p => p.Add(c => c.Page, 1).Add(c => c.NombrePages, 3));

        var boutons = cut.FindAll("button");
        Assert.True(boutons[0].HasAttribute("disabled"));
        Assert.False(boutons[^1].HasAttribute("disabled"));
        Assert.Equal("1", cut.Find("li.active button").TextContent);
        Assert.Equal(5, boutons.Count);
    }

    [Fact]
    public void Suivant_emet_le_numero_de_la_page_suivante()
    {
        var recu = 0;
        var cut = Render<Pagination>(p => p
            .Add(c => c.Page, 2)
            .Add(c => c.NombrePages, 3)
            .Add(c => c.PageChangee, (int page) => recu = page));

        cut.FindAll("button")[^1].Click();

        Assert.Equal(3, recu);
    }

    [Fact]
    public void La_fenetre_reste_limitee_a_cinq_pages()
    {
        var cut = Render<Pagination>(p => p.Add(c => c.Page, 7).Add(c => c.NombrePages, 20));

        var numeros = cut.FindAll("li.page-item button")
            .Select(b => b.TextContent)
            .Where(t => int.TryParse(t, out _))
            .ToList();

        Assert.Equal(["5", "6", "7", "8", "9"], numeros);
    }
}
