using Microsoft.EntityFrameworkCore;
using Textinord.Trame.Domain.Services;

namespace Textinord.Trame.Infrastructure;

/// <summary>Stock lu dans la table Stocks : l'entrepôt le mieux fourni sert la ligne.</summary>
public sealed class StockParTable(TrameDbContext db) : IStockDisponible
{
    public async Task<string?> PremierEntrepotDisponibleAsync(string reference, int quantite, CancellationToken ct = default)
    {
        return await db.Stocks
            .Where(s => s.Reference == reference && s.Quantite >= quantite)
            .OrderByDescending(s => s.Quantite)
            .Select(s => s.Entrepot)
            .FirstOrDefaultAsync(ct);
    }
}
