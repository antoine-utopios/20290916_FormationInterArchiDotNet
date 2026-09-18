using System.Text.RegularExpressions;
using Textinord.Extranet.Modeles;
using Textinord.Extranet.Services;

namespace Textinord.Extranet.Tests.Services;

public partial class CommandeEnMemoireTests
{
    [GeneratedRegex(@"^CMD-\d{4}-\d{6}$")]
    private static partial Regex FormatNumero();

    private static CommandeSaisie Saisie() => new()
    {
        ReferenceClient = "BC-2026-118",
        AdresseLivraison = "12 rue Nationale",
        CodePostal = "59000",
        Ville = "Lille",
        DateLivraisonSouhaitee = DateOnly.FromDateTime(DateTime.Today).AddDays(7),
        AccepteCgv = true
    };

    [Fact]
    public async Task Les_numeros_sont_sequentiels_et_au_format_CMD_AAAA_NNNNNN()
    {
        var service = new CommandeEnMemoire();
        var lignes = new List<LignePanier> { new(CatalogueDeTest.Article("VT-001", "Veste"), 2, 10m) };

        var premiere = await service.PasserAsync(Saisie(), Client.Demonstration, lignes);
        var seconde = await service.PasserAsync(Saisie(), Client.Demonstration, lignes);

        Assert.Matches(FormatNumero(), premiere.Numero);
        Assert.EndsWith("-000001", premiere.Numero);
        Assert.EndsWith("-000002", seconde.Numero);
        Assert.Equal(StatutCommande.Validee, premiere.Statut);
        Assert.Same(premiere, await service.TrouverAsync(premiere.Numero));
    }

    [Fact]
    public async Task Sans_stock_suffisant_dans_un_entrepot_la_commande_attend_le_stock()
    {
        var service = new CommandeEnMemoire();
        var article = CatalogueDeTest.Article("EP-001", "Gants", stockRoubaix: 100, stockLesquin: 50);
        var lignes = new List<LignePanier> { new(article, 120, 10m) };

        var commande = await service.PasserAsync(Saisie(), Client.Demonstration, lignes);

        Assert.Equal(StatutCommande.EnAttenteStock, commande.Statut);
    }

    [Fact]
    public async Task Une_commande_vide_est_refusee()
    {
        var service = new CommandeEnMemoire();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PasserAsync(Saisie(), Client.Demonstration, []));
    }
}
