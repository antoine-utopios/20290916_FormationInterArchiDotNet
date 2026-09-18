using Textinord.Extranet.Modeles;

namespace Textinord.Extranet.Services;

/// <summary>Catalogue en mémoire : filtre, tri et pagination côté serveur.</summary>
public sealed class CatalogueEnMemoire : ICatalogueService
{
    private readonly IReadOnlyList<Article> articles;

    public CatalogueEnMemoire() : this(CatalogueDeDemonstration.Articles)
    {
    }

    /// <remarks>
    /// Paramètre typé IReadOnlyList et non IEnumerable : le conteneur DI sait résoudre
    /// IEnumerable&lt;T&gt; (vide) et choisirait ce constructeur à la place du constructeur
    /// sans paramètre. Le catalogue serait alors vide en production sans aucune erreur.
    /// </remarks>
    public CatalogueEnMemoire(IReadOnlyList<Article> articles)
    {
        this.articles = articles.OrderBy(a => a.Reference, StringComparer.Ordinal).ToList();
    }

    public Task<IReadOnlyList<string>> ListerFamillesAsync(CancellationToken ct = default)
    {
        IReadOnlyList<string> familles = articles
            .Select(a => a.Famille)
            .Distinct()
            .Order()
            .ToList();
        return Task.FromResult(familles);
    }

    public Task<PageResultat<Article>> RechercherAsync(FiltreArticles filtre, int page, int taillePage, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(taillePage, 1);

        IEnumerable<Article> requete = articles;

        if (!string.IsNullOrWhiteSpace(filtre.Texte))
        {
            var texte = filtre.Texte.Trim();
            requete = requete.Where(a =>
                a.Libelle.Contains(texte, StringComparison.OrdinalIgnoreCase) ||
                a.Reference.Contains(texte, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filtre.Famille))
        {
            requete = requete.Where(a => a.Famille == filtre.Famille);
        }

        if (filtre.EnStockSeulement)
        {
            requete = requete.Where(a => a.StockTotal > 0);
        }

        var resultats = requete.ToList();
        var elements = resultats.Skip((page - 1) * taillePage).Take(taillePage).ToList();

        return Task.FromResult(new PageResultat<Article>(elements, page, taillePage, resultats.Count));
    }

    public Task<Article?> TrouverAsync(string reference, CancellationToken ct = default) =>
        Task.FromResult(articles.FirstOrDefault(a => a.Reference.Equals(reference, StringComparison.OrdinalIgnoreCase)));
}
