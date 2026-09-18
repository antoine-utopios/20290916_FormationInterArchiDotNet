namespace Textinord.Trame.Domain.Services;

/// <summary>Port vers le stock : implémenté par l'infrastructure, substitué dans les tests.</summary>
public interface IStockDisponible
{
    /// <summary>
    /// Retourne le code de l'entrepôt qui peut servir la quantité demandée, ou null en rupture
    /// dans les deux entrepôts.
    /// </summary>
    Task<string?> PremierEntrepotDisponibleAsync(string reference, int quantite, CancellationToken ct = default);
}
