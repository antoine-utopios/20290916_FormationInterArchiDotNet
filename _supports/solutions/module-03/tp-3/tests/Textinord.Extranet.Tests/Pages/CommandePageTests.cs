using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Textinord.Extranet.Components.Pages;
using Textinord.Extranet.Modeles;
using Textinord.Extranet.Services;

namespace Textinord.Extranet.Tests.Pages;

public class CommandePageTests : BunitContext
{
    private readonly PanierService panier = new(Client.Demonstration);
    private readonly CommandeEnMemoire commandes = new();

    public CommandePageTests()
    {
        Services.AddSingleton<ICommandeService>(commandes);
        Services.AddScoped(_ => panier);
    }

    [Fact]
    public void Un_panier_vide_ne_montre_pas_le_formulaire()
    {
        var cut = Render<PasserCommande>();

        Assert.Contains("Votre panier est vide", cut.Find(".alert-warning").TextContent);
        Assert.Empty(cut.FindAll("form"));
    }

    [Fact]
    public void Un_formulaire_vide_affiche_les_messages_et_ne_commande_pas()
    {
        panier.Ajouter(CatalogueDeTest.Article("EP-001", "Gants anti-coupure"), 10);
        var navigation = Services.GetRequiredService<NavigationManager>();

        var cut = Render<PasserCommande>();
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            var messages = cut.FindAll(".validation-message").Select(m => m.TextContent).ToList();
            Assert.Contains("Votre référence de commande est obligatoire.", messages);
            Assert.Contains("Le code postal est obligatoire.", messages);
            Assert.Contains("Vous devez accepter les conditions générales de vente.", messages);
        });
        Assert.Equal("http://localhost/", navigation.Uri);
        Assert.Equal(10, panier.NombrePieces);
    }

    [Fact]
    public void Un_formulaire_valide_enregistre_la_commande_vide_le_panier_et_redirige()
    {
        panier.Ajouter(CatalogueDeTest.Article("EP-001", "Gants anti-coupure"), 10);
        var navigation = Services.GetRequiredService<NavigationManager>();
        var dateLivraison = DateOnly.FromDateTime(DateTime.Today).AddDays(7).ToString("yyyy-MM-dd");

        var cut = Render<PasserCommande>();
        cut.Find("#referenceClient").Change("BC-2026-118");
        cut.Find("#adresse").Change("12 rue Nationale");
        cut.Find("#codePostal").Change("59000");
        cut.Find("#ville").Change("Lille");
        cut.Find("#dateLivraison").Change(dateLivraison);
        cut.Find("#cgv").Change(true);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.StartsWith("http://localhost/confirmation/CMD-", navigation.Uri));
        Assert.True(panier.EstVide);
    }

    [Fact]
    public void Un_code_postal_invalide_bloque_la_commande()
    {
        panier.Ajouter(CatalogueDeTest.Article("EP-001", "Gants anti-coupure"), 10);

        var cut = Render<PasserCommande>();
        cut.Find("#codePostal").Change("5900");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
            Assert.Contains("Le code postal comporte cinq chiffres.", cut.FindAll(".validation-message").Select(m => m.TextContent)));
    }
}
