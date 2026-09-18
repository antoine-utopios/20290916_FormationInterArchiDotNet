using Textinord.Extranet.Modeles;

namespace Textinord.Extranet.Tests;

/// <summary>Jeu de données réduit et lisible pour les tests : 30 articles, dont 3 gants.</summary>
public static class CatalogueDeTest
{
    public static Article Article(string reference, string libelle, decimal prix = 12.50m, int stockRoubaix = 100, int stockLesquin = 50, string famille = "EPI textiles") =>
        new(reference, libelle, famille, prix, new Dictionary<string, int> { ["ROU"] = stockRoubaix, ["LES"] = stockLesquin });

    public static IReadOnlyList<Article> TrenteArticles()
    {
        var articles = new List<Article>();
        for (var i = 1; i <= 27; i++)
        {
            articles.Add(Article($"VT-{i:000}", $"Veste de travail modèle {i}", 42.90m, famille: "Vêtements de travail"));
        }

        articles.Add(Article("EP-001", "Gants anti-coupure niveau C", 6.80m));
        articles.Add(Article("EP-002", "Gants manutention enduits", 3.90m));
        articles.Add(Article("EP-003", "Gants soudeur cuir", 9.40m, stockRoubaix: 0, stockLesquin: 0));
        return articles;
    }
}
