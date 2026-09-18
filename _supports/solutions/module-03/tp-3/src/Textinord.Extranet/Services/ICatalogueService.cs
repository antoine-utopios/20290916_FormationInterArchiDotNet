using Textinord.Extranet.Modeles;

namespace Textinord.Extranet.Services;

/// <summary>
/// Accès au catalogue. L'implémentation en mémoire sert de plan B ; l'implémentation
/// HTTP vers l'API Trame 2 (module 5) respectera le même contrat.
/// </summary>
public interface ICatalogueService
{
    Task<IReadOnlyList<string>> ListerFamillesAsync(CancellationToken ct = default);

    Task<PageResultat<Article>> RechercherAsync(FiltreArticles filtre, int page, int taillePage, CancellationToken ct = default);

    Task<Article?> TrouverAsync(string reference, CancellationToken ct = default);
}
