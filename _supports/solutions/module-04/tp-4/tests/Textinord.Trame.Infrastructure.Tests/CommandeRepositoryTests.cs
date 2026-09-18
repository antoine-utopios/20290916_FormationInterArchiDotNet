using Microsoft.EntityFrameworkCore;
using Textinord.Trame.Domain;
using Textinord.Trame.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Textinord.Trame.Infrastructure.Tests;

public sealed class CommandeRepositoryTests : BaseSqliteEnMemoire
{
    [Fact]
    public async Task Ajouter_puis_relire_une_commande_charge_le_client_et_les_lignes()
    {
        var (client, article1, article2) = JeuDeDonnees();
        int id;

        await using (var contexte = CreerContexte())
        {
            contexte.AddRange(client, article1, article2);
            await contexte.SaveChangesAsync();

            var repository = new CommandeRepository(contexte);
            var numero = await repository.ProchainNumeroAsync(2026);
            var commande = new Commande(numero, client, new DateTime(2026, 9, 8, 9, 30, 0, DateTimeKind.Utc),
                new MetadonneesCommande("Extranet", referenceClient: "PO-4471"));
            commande.AjouterLigne(article1, 200);
            commande.AjouterLigne(article2, 10);

            repository.Ajouter(commande);
            await contexte.SauvegarderAsync();
            id = commande.Id;
        }

        await using (var contexte = CreerContexte())
        {
            var relue = await new CommandeRepository(contexte).ObtenirAsync(id);

            Assert.NotNull(relue);
            Assert.Equal("CMD-2026-000001", relue.Numero);
            Assert.Equal("Hôtel Lux Lille", relue.Client!.RaisonSociale);
            Assert.Equal(2, relue.Lignes.Count);
            Assert.Equal(StatutCommande.Brouillon, relue.Statut);
            // 200 x 18,50 à 25 % + 10 x 42 à 25 %
            Assert.Equal(2_775.00m + 315.00m, relue.TotalHt);
        }
    }

    [Fact]
    public async Task Le_numero_est_sequentiel_par_annee()
    {
        var (client, article1, _) = JeuDeDonnees();
        await using var contexte = CreerContexte();
        contexte.AddRange(client, article1);
        await contexte.SaveChangesAsync();
        var repository = new CommandeRepository(contexte);

        var n1 = await repository.ProchainNumeroAsync(2026);
        repository.Ajouter(new Commande(n1, client, DateTime.UtcNow));
        await contexte.SauvegarderAsync();

        var n2 = await repository.ProchainNumeroAsync(2026);
        repository.Ajouter(new Commande(n2, client, DateTime.UtcNow));
        await contexte.SauvegarderAsync();

        var n2027 = await repository.ProchainNumeroAsync(2027);

        Assert.Equal("CMD-2026-000001", n1);
        Assert.Equal("CMD-2026-000002", n2);
        Assert.Equal("CMD-2027-000001", n2027);
    }

    [Fact]
    public async Task Lister_par_statut_ne_suit_pas_les_entites()
    {
        var (client, article1, _) = JeuDeDonnees();
        await using (var contexte = CreerContexte())
        {
            contexte.AddRange(client, article1);
            await contexte.SaveChangesAsync();

            var brouillon = new Commande("CMD-2026-000010", client, DateTime.UtcNow);
            brouillon.AjouterLigne(article1, 5);
            var validee = new Commande("CMD-2026-000011", client, DateTime.UtcNow);
            validee.AjouterLigne(article1, 5);
            validee.Valider(_ => true);
            contexte.AddRange(brouillon, validee);
            await contexte.SaveChangesAsync();
        }

        await using (var contexte = CreerContexte())
        {
            var validees = await new CommandeRepository(contexte).ListerParStatutAsync(StatutCommande.Validee);

            Assert.Single(validees);
            Assert.Equal("CMD-2026-000011", validees[0].Numero);
            Assert.Empty(contexte.ChangeTracker.Entries());
        }
    }

    [Fact]
    public async Task Une_commande_sans_stock_passe_en_attente_de_stock()
    {
        var (client, article1, article2) = JeuDeDonnees();
        await using var contexte = CreerContexte();
        contexte.AddRange(client, article1, article2);
        await contexte.SaveChangesAsync();

        var articles = await contexte.Articles.ToDictionaryAsync(a => a.Id);
        var commande = new Commande("CMD-2026-000020", client, DateTime.UtcNow);
        commande.AjouterLigne(article1, 100);   // 1 200 en stock à Roubaix
        commande.AjouterLigne(article2, 50);    // 40 à Lesquin seulement : insuffisant

        commande.Valider(ligne => articles[ligne.ArticleId].EstDisponible(ligne.Quantite));
        contexte.Add(commande);
        await contexte.SaveChangesAsync();

        var statut = await contexte.Commandes.AsNoTracking()
            .Where(c => c.Numero == "CMD-2026-000020")
            .Select(c => c.Statut)
            .SingleAsync();
        Assert.Equal(StatutCommande.EnAttenteStock, statut);
    }

    [Fact]
    public async Task Le_stock_par_entrepot_est_persiste_dans_sa_propre_table()
    {
        var (_, article1, _) = JeuDeDonnees();
        await using (var contexte = CreerContexte())
        {
            contexte.Add(article1);
            await contexte.SaveChangesAsync();
        }

        await using (var contexte = CreerContexte())
        {
            var relu = await contexte.Articles.SingleAsync(a => a.Reference == "LNG-DRAP-240");

            Assert.Equal(1_500, relu.StockTotal);
            Assert.True(relu.EstDisponible(1_000));
            Assert.False(relu.EstDisponible(1_300));
        }
    }

    [Fact]
    public async Task Annuler_une_commande_en_preparation_est_refuse_par_le_domaine()
    {
        var (client, article1, _) = JeuDeDonnees();
        await using var contexte = CreerContexte();
        contexte.AddRange(client, article1);
        await contexte.SaveChangesAsync();

        var commande = new Commande("CMD-2026-000030", client, DateTime.UtcNow);
        commande.AjouterLigne(article1, 10);
        commande.Valider(_ => true);
        commande.DemarrerPreparation();
        contexte.Add(commande);
        await contexte.SaveChangesAsync();

        var relue = await new CommandeRepository(contexte).ObtenirParNumeroAsync("CMD-2026-000030");
        var erreur = Assert.Throws<RegleMetierException>(() => relue!.Annuler("client injoignable"));
        Assert.Contains("EnPreparation", erreur.Message);
    }

    [Fact]
    public async Task ExecuteUpdate_facture_en_masse_les_commandes_expediees()
    {
        var (client, article1, _) = JeuDeDonnees();
        await using var contexte = CreerContexte();
        contexte.AddRange(client, article1);
        await contexte.SaveChangesAsync();

        for (var i = 1; i <= 3; i++)
        {
            var commande = new Commande(NumeroCommande.Former(2026, 40 + i), client, DateTime.UtcNow);
            commande.AjouterLigne(article1, 10);
            commande.Valider(_ => true);
            commande.DemarrerPreparation();
            commande.Expedier();
            contexte.Add(commande);
        }
        var restante = new Commande("CMD-2026-000050", client, DateTime.UtcNow);
        restante.AjouterLigne(article1, 10);
        contexte.Add(restante);
        await contexte.SaveChangesAsync();

        // Une seule instruction UPDATE, sans charger les entités.
        var modifiees = await contexte.Commandes
            .Where(c => c.Statut == StatutCommande.Expediee)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Statut, StatutCommande.Facturee)
                .SetProperty(c => c.Version, Guid.NewGuid()));

        Assert.Equal(3, modifiees);
        Assert.Equal(3, await contexte.Commandes.AsNoTracking().CountAsync(c => c.Statut == StatutCommande.Facturee));
        Assert.Equal(1, await contexte.Commandes.AsNoTracking().CountAsync(c => c.Statut == StatutCommande.Brouillon));
    }
}
