namespace Textinord.Trame.Domain;

/// <summary>
/// Unit of Work (Fowler) : regroupe les changements d'un cas d'usage en une seule transaction.
/// Dans Trame 2, c'est le DbContext EF Core qui l'implémente : on n'en réécrit pas un.
/// </summary>
public interface IUniteDeTravail
{
    Task<int> SauvegarderAsync(CancellationToken ct = default);
}
