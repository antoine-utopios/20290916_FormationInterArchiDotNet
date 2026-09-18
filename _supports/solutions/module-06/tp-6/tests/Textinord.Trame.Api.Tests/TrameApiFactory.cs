using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Textinord.Trame.Infrastructure;

namespace Textinord.Trame.Api.Tests;

/// <summary>
/// Héberge l'API en mémoire (aucun port ouvert) sur une base SQLite temporaire, propre à la classe de tests.
/// L'initialisation de la base (schéma + jeu de données) est celle de l'application, pas une copie.
/// </summary>
public sealed class TrameApiFactory : WebApplicationFactory<Program>
{
    private readonly string _fichier = Path.Combine(Path.GetTempPath(), $"trame2-tests-{Guid.NewGuid():N}.db");

    public string ChaineDeConnexion => $"Data Source={_fichier}";

    public TrameDbContext CreerContexte() =>
        new(new DbContextOptionsBuilder<TrameDbContext>().UseSqlite(ChaineDeConnexion).Options);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.UseSetting("ConnectionStrings:Trame", ChaineDeConnexion);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            return;
        }

        SqliteConnection.ClearAllPools();
        foreach (var chemin in new[] { _fichier, _fichier + "-wal", _fichier + "-shm" })
        {
            if (File.Exists(chemin))
            {
                File.Delete(chemin);
            }
        }
    }
}
