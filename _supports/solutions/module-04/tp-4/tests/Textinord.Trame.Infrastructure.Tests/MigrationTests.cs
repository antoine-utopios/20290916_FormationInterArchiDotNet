using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Textinord.Trame.Infrastructure.Tests;

public sealed class MigrationTests : BaseSqliteEnMemoire
{
    [Fact]
    public async Task La_migration_initiale_cree_le_schema_et_aucune_migration_ne_reste_en_attente()
    {
        await using var contexte = CreerContexte();

        var appliquees = await contexte.Database.GetAppliedMigrationsAsync();
        var enAttente = await contexte.Database.GetPendingMigrationsAsync();

        Assert.Contains(appliquees, m => m.EndsWith("_Initiale", StringComparison.Ordinal));
        Assert.Empty(enAttente);

        var tables = new List<string>();
        await using var commande = Connexion.CreateCommand();
        commande.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name";
        await using var lecteur = await commande.ExecuteReaderAsync();
        while (await lecteur.ReadAsync())
        {
            tables.Add(lecteur.GetString(0));
        }

        Assert.Contains("Clients", tables);
        Assert.Contains("Articles", tables);
        Assert.Contains("StocksArticle", tables);
        Assert.Contains("Commandes", tables);
        Assert.Contains("LignesCommande", tables);
    }
}
