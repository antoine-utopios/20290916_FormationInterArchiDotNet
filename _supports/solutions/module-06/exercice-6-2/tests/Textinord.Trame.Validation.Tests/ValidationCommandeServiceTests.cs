using NSubstitute.ExceptionExtensions;

namespace Textinord.Trame.Validation.Tests;

public sealed class ValidationCommandeServiceTests
{
    private static readonly DateTimeOffset Instant = new(2026, 9, 7, 9, 30, 0, TimeSpan.FromHours(2));

    private static readonly Client HotelLeBeffroi = new("C-0001", "Hôtel Le Beffroi, Lille", 0.10m, Actif: true);
    private static readonly Client ChantiersVandamme = new("C-0117", "Chantiers Vandamme SA", 0.25m, Actif: true);
    private static readonly Client ConfectionDubrulle = new("C-0900", "Confection Dubrulle", 0.05m, Actif: false);

    private readonly IReferentielClients _clients = Substitute.For<IReferentielClients>();
    private readonly IStockDisponible _stock = Substitute.For<IStockDisponible>();
    private readonly INotificateurAdv _notificateur = Substitute.For<INotificateurAdv>();
    private readonly TimeProvider _horloge = Substitute.For<TimeProvider>();
    private readonly ValidationCommandeService _service;

    public ValidationCommandeServiceTests()
    {
        _clients.TrouverAsync(HotelLeBeffroi.Code, Arg.Any<CancellationToken>()).Returns(HotelLeBeffroi);
        _clients.TrouverAsync(ChantiersVandamme.Code, Arg.Any<CancellationToken>()).Returns(ChantiersVandamme);
        _clients.TrouverAsync(ConfectionDubrulle.Code, Arg.Any<CancellationToken>()).Returns(ConfectionDubrulle);
        _horloge.GetUtcNow().Returns(Instant);
        _service = new ValidationCommandeService(_clients, _stock, _notificateur, _horloge);
    }

    private static Commande CommandeDe(string codeClient, params (string Reference, int Quantite)[] lignes)
    {
        var commande = new Commande("CMD-2026-000123", codeClient);
        commande.Lignes.AddRange(lignes.Select(l => new LigneCommande(l.Reference, l.Quantite, 10m)));
        return commande;
    }

    private void StockPartout()
    {
        _stock.EntrepotsDisponiblesAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "RBX", "LSQ" });
    }

    // ---- Tests fournis dans l'énoncé --------------------------------------------------

    [Fact]
    public async Task Refuse_une_commande_dont_le_client_est_inactif()
    {
        var commande = CommandeDe(ConfectionDubrulle.Code, ("VT-1001", 10));

        var resultat = await _service.ValiderAsync(commande);

        Assert.Equal(StatutCommande.Refusee, resultat.Statut);
        Assert.Contains("inactif", resultat.Motifs.Single());
        Assert.Equal(StatutCommande.Refusee, commande.Statut);
    }

    [Fact]
    public async Task Valide_une_commande_dont_toutes_les_lignes_sont_en_stock()
    {
        StockPartout();
        var commande = CommandeDe(HotelLeBeffroi.Code, ("VT-1001", 10), ("LH-3300", 40));

        var resultat = await _service.ValiderAsync(commande);

        Assert.True(resultat.EstValidee);
        Assert.Empty(resultat.Motifs);
        Assert.Equal(StatutCommande.Validee, commande.Statut);
    }

    // ---- Tests à écrire (corrigé) ------------------------------------------------------

    [Fact]
    public async Task Refuse_un_client_inconnu_sans_interroger_le_stock()
    {
        _clients.TrouverAsync("C-9999", Arg.Any<CancellationToken>()).Returns((Client?)null);
        var commande = CommandeDe("C-9999", ("VT-1001", 10));

        var resultat = await _service.ValiderAsync(commande);

        Assert.Equal(StatutCommande.Refusee, resultat.Statut);
        Assert.Contains("Client inconnu", resultat.Motifs.Single());
        await _stock.DidNotReceiveWithAnyArgs().EntrepotsDisponiblesAsync(default!, default, default);
    }

    [Fact]
    public async Task Refuse_une_ligne_a_quantite_nulle_ou_negative_sans_interroger_le_stock()
    {
        var commande = CommandeDe(HotelLeBeffroi.Code, ("VT-1001", 0), ("LH-3300", -3));

        var resultat = await _service.ValiderAsync(commande);

        Assert.Equal(StatutCommande.Refusee, resultat.Statut);
        Assert.Equal(2, resultat.Motifs.Count);
        Assert.All(resultat.Motifs, m => Assert.Contains("Quantité invalide", m));
        await _stock.DidNotReceiveWithAnyArgs().EntrepotsDisponiblesAsync(default!, default, default);
    }

    [Fact]
    public async Task Refuse_une_commande_sans_ligne()
    {
        var commande = CommandeDe(HotelLeBeffroi.Code);

        var resultat = await _service.ValiderAsync(commande);

        Assert.Equal(StatutCommande.Refusee, resultat.Statut);
        Assert.Contains("au moins une ligne", resultat.Motifs.Single());
    }

    [Fact]
    public async Task Passe_en_attente_de_stock_et_alerte_l_adv_pour_chaque_rupture()
    {
        _stock.EntrepotsDisponiblesAsync("VT-1001", 10, Arg.Any<CancellationToken>()).Returns(new[] { "RBX" });
        _stock.EntrepotsDisponiblesAsync("VT-1050", 5, Arg.Any<CancellationToken>()).Returns(Array.Empty<string>());
        _stock.EntrepotsDisponiblesAsync("EPI-2040", 200, Arg.Any<CancellationToken>()).Returns(Array.Empty<string>());
        var commande = CommandeDe(HotelLeBeffroi.Code, ("VT-1001", 10), ("VT-1050", 5), ("EPI-2040", 200));

        var resultat = await _service.ValiderAsync(commande);

        Assert.Equal(StatutCommande.EnAttenteStock, resultat.Statut);
        Assert.Equal(["Rupture de stock : VT-1050.", "Rupture de stock : EPI-2040."], resultat.Motifs);
        Assert.Equal(StatutCommande.EnAttenteStock, commande.Statut);
        Assert.Null(commande.ValideeLe);
        await _notificateur.Received(1).SignalerRuptureAsync("CMD-2026-000123", "VT-1050", Arg.Any<CancellationToken>());
        await _notificateur.Received(1).SignalerRuptureAsync("CMD-2026-000123", "EPI-2040", Arg.Any<CancellationToken>());
        await _notificateur.ReceivedWithAnyArgs(2).SignalerRuptureAsync(default!, default!, default);
    }

    [Fact]
    public async Task Affecte_le_premier_entrepot_disponible_a_chaque_ligne()
    {
        _stock.EntrepotsDisponiblesAsync("VT-1001", 10, Arg.Any<CancellationToken>()).Returns(new[] { "LSQ", "RBX" });
        _stock.EntrepotsDisponiblesAsync("LH-3300", 40, Arg.Any<CancellationToken>()).Returns(new[] { "RBX" });
        var commande = CommandeDe(HotelLeBeffroi.Code, ("VT-1001", 10), ("LH-3300", 40));

        await _service.ValiderAsync(commande);

        Assert.Equal(["LSQ", "RBX"], commande.Lignes.Select(l => l.Entrepot));
        await _stock.Received(1).EntrepotsDisponiblesAsync("VT-1001", 10, Arg.Any<CancellationToken>());
        await _stock.Received(1).EntrepotsDisponiblesAsync("LH-3300", 40, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("C-0001", 100, 0.10)]   // 10 % client, pas de volume
    [InlineData("C-0001", 600, 0.15)]   // 10 % + 5 % volume
    [InlineData("C-0117", 600, 0.30)]   // 25 % + 5 % = 30 %, plafond atteint
    public async Task Applique_la_remise_client_plus_volume_plafonnee_a_30_pour_cent(string codeClient, int quantite, double attendu)
    {
        StockPartout();
        var commande = CommandeDe(codeClient, ("VT-1001", quantite));

        await _service.ValiderAsync(commande);

        Assert.Equal((decimal)attendu, commande.Lignes.Single().TauxRemise);
    }

    [Fact]
    public async Task Date_la_validation_avec_l_horloge_injectee()
    {
        StockPartout();
        var commande = CommandeDe(HotelLeBeffroi.Code, ("VT-1001", 10));

        await _service.ValiderAsync(commande);

        Assert.Equal(Instant, commande.ValideeLe);
        _horloge.Received(1).GetUtcNow();
    }

    [Fact]
    public async Task Consulte_le_client_avant_le_stock()
    {
        StockPartout();
        var commande = CommandeDe(HotelLeBeffroi.Code, ("VT-1001", 10));

        await _service.ValiderAsync(commande);

        Received.InOrder(async () =>
        {
            await _clients.TrouverAsync(HotelLeBeffroi.Code, Arg.Any<CancellationToken>());
            await _stock.EntrepotsDisponiblesAsync("VT-1001", 10, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Propage_l_exception_du_referentiel_et_laisse_la_commande_en_brouillon()
    {
        _clients.TrouverAsync("C-0001", Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("SQL Server injoignable"));
        var commande = CommandeDe(HotelLeBeffroi.Code, ("VT-1001", 10));

        var exception = await Assert.ThrowsAsync<TimeoutException>(() => _service.ValiderAsync(commande));

        Assert.Contains("injoignable", exception.Message);
        Assert.Equal(StatutCommande.Brouillon, commande.Statut);
        await _notificateur.DidNotReceiveWithAnyArgs().SignalerRuptureAsync(default!, default!, default);
    }
}
