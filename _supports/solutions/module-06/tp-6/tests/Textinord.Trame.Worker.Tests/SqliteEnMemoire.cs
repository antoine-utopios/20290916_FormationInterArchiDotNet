using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Textinord.Trame.Infrastructure;

namespace Textinord.Trame.Worker.Tests;

/// <summary>
/// Base SQLite en mémoire : elle vit tant que la connexion reste ouverte.
/// Chaque test en crée une, isolée des autres, sans Docker.
/// </summary>
public sealed class SqliteEnMemoire : IDisposable
{
    public SqliteEnMemoire()
    {
        Connexion = new SqliteConnection("Data Source=:memory:");
        Connexion.Open();
        using var db = CreerContexte();
        db.Database.EnsureCreated();
    }

    public SqliteConnection Connexion { get; }

    public DbContextOptions<TrameDbContext> Options =>
        new DbContextOptionsBuilder<TrameDbContext>().UseSqlite(Connexion).Options;

    public TrameDbContext CreerContexte() => new(Options);

    public void Dispose() => Connexion.Dispose();
}
