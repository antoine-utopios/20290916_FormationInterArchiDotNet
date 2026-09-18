using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Textinord.Trame.Api.Commandes;
using Textinord.Trame.Domain;
using Textinord.Trame.Domain.Services;
using Textinord.Trame.Infrastructure;

namespace Textinord.Trame.Api.Tests;

/// <summary>Le port stock est substitué : on vérifie ce que le cas d'usage lui demande.</summary>
public sealed class PriseDeCommandeTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Instant = new(2026, 9, 7, 10, 0, 0, TimeSpan.FromHours(2));

    private readonly SqliteConnection _connexion = new("Data Source=:memory:");
    private readonly IStockDisponible _stock = Substitute.For<IStockDisponible>();
    private readonly TimeProvider _horloge = Substitute.For<TimeProvider>();

    public async Task InitializeAsync()
    {
        _horloge.GetUtcNow().Returns(Instant);
        await _connexion.OpenAsync();
        await using var db = CreerContexte();
        await db.Database.EnsureCreatedAsync();
        await DonneesInitiales.InsererAsync(db);
    }

    public async Task DisposeAsync() => await _connexion.DisposeAsync();

    private TrameDbContext CreerContexte() =>
        new(new DbContextOptionsBuilder<TrameDbContext>().UseSqlite(_connexion).Options);

    [Fact]
    public async Task Interroge_le_stock_pour_chaque_ligne_et_affecte_l_entrepot_retourne()
    {
        _stock.PremierEntrepotDisponibleAsync("VT-1001", 10, Arg.Any<CancellationToken>()).Returns(Entrepots.Lesquin);
        _stock.PremierEntrepotDisponibleAsync("LH-3300", 25, Arg.Any<CancellationToken>()).Returns(Entrepots.Roubaix);

        await using var db = CreerContexte();
        var service = new PriseDeCommande(db, _stock, _horloge);
        var resultat = await service.EnregistrerAsync(
            new NouvelleCommandeRequete("C-0001", [new LigneRequete("VT-1001", 10), new LigneRequete("LH-3300", 25)]));

        Assert.True(resultat.EstSucces);
        var commande = resultat.Valeur!;
        Assert.Equal(StatutCommande.Validee, commande.Statut);
        Assert.Equal(["LSQ", "RBX"], commande.Lignes.Select(l => l.EntrepotAffecte));
        await _stock.Received(1).PremierEntrepotDisponibleAsync("VT-1001", 10, Arg.Any<CancellationToken>());
        await _stock.Received(1).PremierEntrepotDisponibleAsync("LH-3300", 25, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Numerote_les_commandes_sequentiellement_dans_l_annee()
    {
        _stock.PremierEntrepotDisponibleAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Entrepots.Roubaix);

        await using var db = CreerContexte();
        var service = new PriseDeCommande(db, _stock, _horloge);
        var premiere = await service.EnregistrerAsync(new NouvelleCommandeRequete("C-0001", [new LigneRequete("VT-1001", 1)]));
        var seconde = await service.EnregistrerAsync(new NouvelleCommandeRequete("C-0042", [new LigneRequete("VT-1001", 2)]));

        Assert.Equal("CMD-2026-000001", premiere.Valeur!.Numero);
        Assert.Equal("CMD-2026-000002", seconde.Valeur!.Numero);
    }

    [Fact]
    public async Task Refuse_un_article_inconnu_sans_interroger_le_stock()
    {
        await using var db = CreerContexte();
        var service = new PriseDeCommande(db, _stock, _horloge);

        var resultat = await service.EnregistrerAsync(new NouvelleCommandeRequete("C-0001", [new LigneRequete("XX-9999", 1)]));

        Assert.False(resultat.EstSucces);
        Assert.Contains(resultat.Erreurs, e => e.Contains("Article inconnu"));
        await _stock.DidNotReceiveWithAnyArgs().PremierEntrepotDisponibleAsync(default!, default, default);
        Assert.Equal(0, await db.Commandes.CountAsync());
    }
}
