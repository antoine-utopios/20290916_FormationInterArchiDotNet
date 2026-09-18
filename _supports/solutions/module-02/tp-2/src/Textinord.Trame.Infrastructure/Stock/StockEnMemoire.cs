using System.Collections.Concurrent;
using Textinord.Trame.Domain.Commandes;

namespace Textinord.Trame.Infrastructure.Stock;

/// <summary>
/// Disponibilité de stock en mémoire : tout est disponible sauf les références déclarées en rupture.
/// Suffit pour le TP ; la vraie consultation de stock (Cosmos DB, Redis) vient au module 4.
/// </summary>
public sealed class StockEnMemoire : IDisponibiliteStock
{
    private readonly ConcurrentDictionary<string, int> _disponible = new(StringComparer.OrdinalIgnoreCase);

    public void DeclarerDisponible(string reference, int quantite) => _disponible[reference] = quantite;

    public bool EstDisponible(string reference, int quantite) =>
        !_disponible.TryGetValue(reference, out var stock) || stock >= quantite;
}
