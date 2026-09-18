using System.Data.Common;
using Dapper;

namespace Textinord.Trame.Infrastructure.Lecture;

/// <summary>
/// Requête de lecture en SQL avec Dapper, sur la même connexion que le DbContext.
/// Le SQL est portable SQLite / SQL Server (pas de fonction propriétaire).
/// </summary>
public sealed class CommandeLectureDapper(DbConnection connexion) : ICommandeLecture
{
    private const string Sql = """
        SELECT c.Numero,
               cl.RaisonSociale                        AS Client,
               c.Statut,
               COUNT(l.Id)                             AS NombreLignes,
               COALESCE(SUM(l.Quantite * l.PrixUnitaire
                            * (1 - l.RemisePourcent / 100.0)), 0) AS TotalHt
        FROM Commandes c
        JOIN Clients cl ON cl.Id = c.ClientId
        LEFT JOIN LignesCommande l ON l.CommandeId = c.Id
        WHERE c.Date >= @Debut AND c.Date < @Fin
        GROUP BY c.Numero, cl.RaisonSociale, c.Statut
        ORDER BY c.Numero
        """;

    public async Task<IReadOnlyList<CommandeResume>> ResumesDuJourAsync(DateOnly jour, CancellationToken ct = default)
    {
        var debut = jour.ToDateTime(TimeOnly.MinValue);
        var fin = debut.AddDays(1);

        var commande = new CommandDefinition(Sql, new { Debut = debut, Fin = fin }, cancellationToken: ct);
        var lignes = await connexion.QueryAsync<CommandeResumeBrut>(commande);

        return lignes
            .Select(l => new CommandeResume(l.Numero, l.Client, l.Statut, (int)l.NombreLignes, Math.Round((decimal)l.TotalHt, 2)))
            .ToList();
    }

    // Projection brute : SQLite renvoie COUNT en INTEGER 64 bits (long) et les agrégats décimaux en REAL (double).
    private sealed record CommandeResumeBrut(string Numero, string Client, string Statut, long NombreLignes, double TotalHt);
}
