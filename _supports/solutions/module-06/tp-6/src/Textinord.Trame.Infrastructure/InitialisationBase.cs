using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Textinord.Trame.Infrastructure;

/// <summary>
/// Crée le schéma SQLite au démarrage (EnsureCreated : suffisant pour le TP ; en production,
/// les migrations EF Core du module 4 s'appliquent dans le pipeline, pas au démarrage).
/// </summary>
public sealed class InitialisationBase(IServiceScopeFactory scopes, ILogger<InitialisationBase> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrameDbContext>();

        var chaine = db.Database.GetConnectionString();
        var fichier = chaine is null ? null : new SqliteConnectionStringBuilder(chaine).DataSource;
        if (fichier is not null
            && !fichier.Contains("memory", StringComparison.OrdinalIgnoreCase)
            && Path.GetDirectoryName(fichier) is { Length: > 0 } dossier)
        {
            Directory.CreateDirectory(dossier);
        }

        await db.Database.EnsureCreatedAsync(cancellationToken);
        // Journal WAL : l'API et le Worker lisent et écrivent le même fichier sans se bloquer.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
        await DonneesInitiales.InsererAsync(db, cancellationToken);

        logger.LogInformation("Base Trame 2 prête ({Source})", fichier ?? "connexion fournie");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
