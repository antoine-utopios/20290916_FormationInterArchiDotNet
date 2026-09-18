using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Textinord.Trame.Domain;
using Textinord.Trame.Infrastructure.Persistence;

namespace Textinord.Trame.Infrastructure.Tests;

/// <summary>
/// Socle des tests d'intégration : une base SQLite en mémoire par test, créée par la migration
/// initiale (on teste donc aussi la migration). La connexion reste ouverte le temps du test :
/// une base « :memory: » disparaît dès que sa dernière connexion se ferme.
/// </summary>
public abstract class BaseSqliteEnMemoire : IDisposable
{
    private readonly SqliteConnection _connexion;

    protected BaseSqliteEnMemoire()
    {
        _connexion = new SqliteConnection("Data Source=:memory:");
        _connexion.Open();

        using var contexte = CreerContexte();
        contexte.Database.Migrate();
    }

    /// <summary>Un nouveau DbContext sur la même base : simule deux requêtes HTTP, deux utilisateurs.</summary>
    protected TrameDbContext CreerContexte()
    {
        var options = new DbContextOptionsBuilder<TrameDbContext>()
            .UseSqlite(_connexion)
            .Options;
        return new TrameDbContext(options);
    }

    protected SqliteConnection Connexion => _connexion;

    /// <summary>Jeu de données Textinord minimal : un client grand compte, deux articles, deux entrepôts.</summary>
    protected static (Client client, Article article1, Article article2) JeuDeDonnees()
    {
        var client = new Client("HOTELUX", "Hôtel Lux Lille", ConditionTarifaire.GrandCompte);

        var article1 = new Article("LNG-DRAP-240", "Drap plat 240 x 300 blanc", "Linge", 18.50m);
        article1.DefinirStock("RBX", 1_200);
        article1.DefinirStock("LSQ", 300);

        var article2 = new Article("EPI-VEST-HV", "Veste haute visibilité classe 2", "EPI", 42.00m);
        article2.DefinirStock("RBX", 0);
        article2.DefinirStock("LSQ", 40);

        return (client, article1, article2);
    }

    public void Dispose()
    {
        _connexion.Dispose();
        GC.SuppressFinalize(this);
    }
}
