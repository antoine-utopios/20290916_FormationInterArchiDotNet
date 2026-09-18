using Microsoft.EntityFrameworkCore;
using Textinord.Trame.Domain;

namespace Textinord.Trame.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository de l'agrégat Commande. Il encapsule les requêtes EF Core spécifiques
/// (chargement des lignes et du client, numérotation) ; il ne sauvegarde jamais lui-même.
/// </summary>
public sealed class CommandeRepository(TrameDbContext context) : ICommandeRepository
{
    public Task<Commande?> ObtenirAsync(int id, CancellationToken ct = default) =>
        context.Commandes
            .Include(c => c.Client)
            .Include(c => c.Lignes)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Commande?> ObtenirParNumeroAsync(string numero, CancellationToken ct = default) =>
        context.Commandes
            .Include(c => c.Client)
            .Include(c => c.Lignes)
            .FirstOrDefaultAsync(c => c.Numero == numero, ct);

    /// <summary>
    /// Numérotation séquentielle par année. Version pédagogique : lecture du dernier numéro.
    /// En production sur SQL Server, on préférera une SEQUENCE par année ou une table de compteurs
    /// verrouillée dans la transaction (voir solution-tp-4-persistance-trame2.md).
    /// </summary>
    public async Task<string> ProchainNumeroAsync(int annee, CancellationToken ct = default)
    {
        var prefixe = $"{NumeroCommande.Prefixe}{annee:D4}-";
        var dernier = await context.Commandes
            .AsNoTracking()
            .Where(c => c.Numero.StartsWith(prefixe))
            .OrderByDescending(c => c.Numero)
            .Select(c => c.Numero)
            .FirstOrDefaultAsync(ct);

        var sequence = dernier is null ? 1 : NumeroCommande.Sequence(dernier) + 1;
        return NumeroCommande.Former(annee, sequence);
    }

    public async Task<IReadOnlyList<Commande>> ListerParStatutAsync(StatutCommande statut, CancellationToken ct = default) =>
        await context.Commandes
            .AsNoTracking()
            .Include(c => c.Client)
            .Include(c => c.Lignes)
            .Where(c => c.Statut == statut)
            .OrderBy(c => c.Numero)
            .ToListAsync(ct);

    public void Ajouter(Commande commande) => context.Commandes.Add(commande);
}
