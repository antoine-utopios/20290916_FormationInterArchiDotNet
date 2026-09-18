using System.ComponentModel;
using NSubstitute;
using Textinord.Saisie.ViewModels.Modeles;
using Textinord.Saisie.ViewModels.Services;

namespace Textinord.Saisie.ViewModels.Tests;

public class SaisieCommandeViewModelTests
{
    private static readonly Client HotelBeaulieu = new("HOT-0421", "Hôtel Beaulieu Lille", 10m);
    private static readonly Client Collectivite = new("COL-0007", "Ville de Roubaix", 25m);
    private static readonly Article Gants = new("EP-001", "Gants anti-coupure niveau C", 6.80m);
    private static readonly Article Veste = new("VT-001", "Veste de travail bleu marine", 42.90m);

    private readonly IClientService clients = Substitute.For<IClientService>();
    private readonly ICatalogueService catalogue = Substitute.For<ICatalogueService>();
    private readonly IStockService stocks = Substitute.For<IStockService>();
    private readonly ICommandeService commandes = Substitute.For<ICommandeService>();

    private SaisieCommandeViewModel Creer()
    {
        clients.TrouverAsync("HOT-0421", Arg.Any<CancellationToken>()).Returns(HotelBeaulieu);
        clients.TrouverAsync("COL-0007", Arg.Any<CancellationToken>()).Returns(Collectivite);
        catalogue.ListerAsync(Arg.Any<CancellationToken>()).Returns([Gants, Veste]);
        stocks.StockDisponibleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(1000);
        commandes.EnregistrerAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<LigneCommande>>(), Arg.Any<CancellationToken>())
            .Returns("CMD-2026-000042");
        return new SaisieCommandeViewModel(clients, catalogue, stocks, commandes);
    }

    [Fact]
    public async Task Charger_remplit_la_liste_des_articles()
    {
        var vm = Creer();

        await vm.ChargerAsync();

        Assert.Equal(2, vm.Articles.Count);
        Assert.Equal("EP-001", vm.Articles[0].Reference);
    }

    [Fact]
    public void Sans_code_client_la_recherche_est_impossible()
    {
        var vm = Creer();

        Assert.False(vm.RechercherClientCommand.CanExecute(null));

        vm.CodeClient = "HOT-0421";

        Assert.True(vm.RechercherClientCommand.CanExecute(null));
    }

    [Fact]
    public async Task Un_client_inconnu_produit_un_message_et_bloque_l_ajout()
    {
        var vm = Creer();
        vm.CodeClient = "XXX-9999";

        await vm.RechercherClientCommand.ExecuteAsync(null);

        Assert.Null(vm.ClientCourant);
        Assert.Equal("Client XXX-9999 inconnu.", vm.MessageErreur);
        Assert.Equal("Aucun client sélectionné", vm.RaisonSociale);
        vm.ArticleSelectionne = Gants;
        Assert.False(vm.AjouterLigneCommand.CanExecute(null));
    }

    [Fact]
    public async Task Un_client_connu_est_charge_avec_sa_raison_sociale()
    {
        var vm = Creer();
        var notifications = new List<string?>();
        vm.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        vm.CodeClient = " HOT-0421 ";

        await vm.RechercherClientCommand.ExecuteAsync(null);

        Assert.Equal(HotelBeaulieu, vm.ClientCourant);
        Assert.Equal("Hôtel Beaulieu Lille", vm.RaisonSociale);
        Assert.Contains(nameof(SaisieCommandeViewModel.RaisonSociale), notifications);
        Assert.Null(vm.MessageErreur);
    }

    [Fact]
    public async Task L_ajout_est_impossible_sans_article_ou_avec_une_quantite_nulle()
    {
        var vm = Creer();
        vm.CodeClient = "HOT-0421";
        await vm.RechercherClientCommand.ExecuteAsync(null);

        Assert.False(vm.AjouterLigneCommand.CanExecute(null));

        vm.ArticleSelectionne = Gants;
        Assert.True(vm.AjouterLigneCommand.CanExecute(null));

        vm.Quantite = 0;
        Assert.False(vm.AjouterLigneCommand.CanExecute(null));
    }

    [Fact]
    public async Task Le_stock_insuffisant_refuse_la_ligne()
    {
        var vm = Creer();
        stocks.StockDisponibleAsync("EP-001", Arg.Any<CancellationToken>()).Returns(40);
        vm.CodeClient = "HOT-0421";
        await vm.RechercherClientCommand.ExecuteAsync(null);
        vm.ArticleSelectionne = Gants;
        vm.Quantite = 50;

        await vm.AjouterLigneCommand.ExecuteAsync(null);

        Assert.Empty(vm.Lignes);
        Assert.Equal("Stock insuffisant pour EP-001 : 40 disponible(s), 50 demandé(s).", vm.MessageErreur);
    }

    [Fact]
    public async Task La_remise_cumule_client_et_volume_avec_un_plafond_de_30_pourcent()
    {
        var vm = Creer();
        var totalNotifie = false;
        vm.PropertyChanged += (_, e) => totalNotifie |= e.PropertyName == nameof(SaisieCommandeViewModel.Total);
        vm.CodeClient = "COL-0007";
        await vm.RechercherClientCommand.ExecuteAsync(null);
        vm.ArticleSelectionne = Gants;
        vm.Quantite = 600;

        await vm.AjouterLigneCommand.ExecuteAsync(null);

        var ligne = Assert.Single(vm.Lignes);
        Assert.Equal(30m, ligne.TauxRemise);
        Assert.Equal(2856m, vm.Total);          // 600 x 6,80 = 4 080 ; - 30 % = 2 856
        Assert.True(totalNotifie);
        Assert.Equal(1, vm.Quantite);           // la quantité est réinitialisée pour la ligne suivante
    }

    [Fact]
    public async Task Valider_enregistre_les_lignes_puis_vide_la_saisie()
    {
        var vm = Creer();
        vm.CodeClient = "HOT-0421";
        await vm.RechercherClientCommand.ExecuteAsync(null);
        Assert.False(vm.ValiderCommand.CanExecute(null));
        vm.ArticleSelectionne = Veste;
        vm.Quantite = 12;
        await vm.AjouterLigneCommand.ExecuteAsync(null);
        Assert.True(vm.ValiderCommand.CanExecute(null));

        await vm.ValiderCommand.ExecuteAsync(null);

        await commandes.Received(1).EnregistrerAsync(
            "HOT-0421",
            Arg.Is<IReadOnlyList<LigneCommande>>(l => l.Count == 1 && l[0].Quantite == 12 && l[0].TauxRemise == 10m),
            Arg.Any<CancellationToken>());
        Assert.Equal("CMD-2026-000042", vm.NumeroCommande);
        Assert.Empty(vm.Lignes);
        Assert.Equal(0m, vm.Total);
        Assert.False(vm.ValiderCommand.CanExecute(null));
    }

    [Fact]
    public async Task Retirer_une_ligne_recalcule_le_total()
    {
        var vm = Creer();
        vm.CodeClient = "HOT-0421";
        await vm.RechercherClientCommand.ExecuteAsync(null);
        vm.ArticleSelectionne = Gants;
        vm.Quantite = 100;
        await vm.AjouterLigneCommand.ExecuteAsync(null);
        vm.ArticleSelectionne = Veste;
        vm.Quantite = 10;
        await vm.AjouterLigneCommand.ExecuteAsync(null);
        Assert.Equal(612m + 386.10m, vm.Total);

        vm.RetirerLigneCommand.Execute(vm.Lignes[0]);

        var restante = Assert.Single(vm.Lignes);
        Assert.Equal("VT-001", restante.Article.Reference);
        Assert.Equal(386.10m, vm.Total);
    }
}
