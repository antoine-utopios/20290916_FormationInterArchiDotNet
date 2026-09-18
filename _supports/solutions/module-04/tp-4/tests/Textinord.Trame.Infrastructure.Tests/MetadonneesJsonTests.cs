using Microsoft.EntityFrameworkCore;
using Textinord.Trame.Domain;
using Xunit;

namespace Textinord.Trame.Infrastructure.Tests;

public sealed class MetadonneesJsonTests : BaseSqliteEnMemoire
{
    [Fact]
    public async Task Les_metadonnees_sont_stockees_en_json_et_relues_a_l_identique()
    {
        var (client, article1, _) = JeuDeDonnees();
        int id;
        await using (var contexte = CreerContexte())
        {
            contexte.AddRange(client, article1);
            await contexte.SaveChangesAsync();

            var commande = new Commande("CMD-2026-000200", client, DateTime.UtcNow,
                new MetadonneesCommande("EDI", "EDI-2026-88", "Livraison quai 3", ["prioritaire", "grand-compte"]));
            commande.AjouterLigne(article1, 12);
            contexte.Add(commande);
            await contexte.SaveChangesAsync();
            id = commande.Id;
        }

        await using var commandeSql = Connexion.CreateCommand();
        commandeSql.CommandText = "SELECT Metadonnees FROM Commandes WHERE Id = $id";
        commandeSql.Parameters.AddWithValue("$id", id);
        var json = (string)(await commandeSql.ExecuteScalarAsync())!;

        Assert.Contains("\"Origine\":\"EDI\"", json);
        Assert.Contains("\"Etiquettes\":[\"prioritaire\",\"grand-compte\"]", json);

        await using (var contexte = CreerContexte())
        {
            var relue = await contexte.Commandes.AsNoTracking().SingleAsync(c => c.Id == id);

            Assert.Equal("EDI", relue.Metadonnees.Origine);
            Assert.Equal("EDI-2026-88", relue.Metadonnees.ReferenceClient);
            Assert.Equal("Livraison quai 3", relue.Metadonnees.Commentaire);
            Assert.Equal(["prioritaire", "grand-compte"], relue.Metadonnees.Etiquettes);
        }
    }

    [Fact]
    public async Task On_peut_filtrer_sur_une_propriete_json_en_linq()
    {
        var (client, article1, _) = JeuDeDonnees();
        await using var contexte = CreerContexte();
        contexte.AddRange(client, article1);
        await contexte.SaveChangesAsync();

        contexte.AddRange(
            new Commande("CMD-2026-000210", client, DateTime.UtcNow, new MetadonneesCommande("Extranet")),
            new Commande("CMD-2026-000211", client, DateTime.UtcNow, new MetadonneesCommande("EDI")),
            new Commande("CMD-2026-000212", client, DateTime.UtcNow, new MetadonneesCommande("Extranet")));
        await contexte.SaveChangesAsync();

        // Traduit en SQL sur la colonne JSON (json_extract sur SQLite, JSON_VALUE sur SQL Server).
        var numeros = await contexte.Commandes.AsNoTracking()
            .Where(c => c.Metadonnees.Origine == "Extranet")
            .OrderBy(c => c.Numero)
            .Select(c => c.Numero)
            .ToListAsync();

        Assert.Equal(["CMD-2026-000210", "CMD-2026-000212"], numeros);
    }
}
