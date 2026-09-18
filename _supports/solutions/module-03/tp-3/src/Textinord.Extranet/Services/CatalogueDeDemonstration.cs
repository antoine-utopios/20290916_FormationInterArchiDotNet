using Textinord.Extranet.Modeles;

namespace Textinord.Extranet.Services;

/// <summary>
/// Jeu de données déterministe : 3 familles x 8 modèles x 5 tailles = 120 articles,
/// stocks répartis entre Roubaix (ROU) et Lesquin (LES). Quelques ruptures volontaires.
/// </summary>
public static class CatalogueDeDemonstration
{
    private static readonly string[] Tailles = ["S", "M", "L", "XL", "XXL"];

    private static readonly (string Prefixe, string Famille, (string Libelle, decimal Prix)[] Modeles)[] Familles =
    [
        ("VT", "Vêtements de travail",
        [
            ("Veste de travail bleu marine", 42.90m),
            ("Pantalon multipoches gris", 38.50m),
            ("Combinaison de travail", 59.00m),
            ("Blouse blanche coton", 27.40m),
            ("Polo manches courtes marine", 18.90m),
            ("Parka hiver doublée", 89.00m),
            ("Gilet sans manches multipoches", 34.20m),
            ("Bermuda de travail", 29.90m)
        ]),
        ("EP", "EPI textiles",
        [
            ("Gants anti-coupure niveau C", 6.80m),
            ("Gilet haute visibilité jaune", 9.50m),
            ("Veste haute visibilité orange", 44.00m),
            ("Pantalon ignifugé", 74.90m),
            ("Gants manutention enduits", 3.90m),
            ("Tablier de soudeur cuir", 52.00m),
            ("Manchettes anti-coupure", 11.20m),
            ("Cagoule ignifugée", 15.60m)
        ]),
        ("LH", "Linge hôtellerie",
        [
            ("Drap plat 240 x 300 blanc", 24.00m),
            ("Housse de couette 260 x 240", 31.50m),
            ("Serviette éponge 50 x 100", 5.40m),
            ("Drap de bain 100 x 150", 12.80m),
            ("Peignoir éponge col châle", 29.90m),
            ("Nappe damassée 180 x 180", 22.70m),
            ("Taie d'oreiller 65 x 65", 4.90m),
            ("Tapis de bain 50 x 80", 8.60m)
        ])
    ];

    public static IReadOnlyList<Article> Articles { get; } = Generer();

    private static List<Article> Generer()
    {
        var aleatoire = new Random(20260907);
        var articles = new List<Article>(120);

        foreach (var (prefixe, famille, modeles) in Familles)
        {
            var numero = 1;
            foreach (var (libelle, prix) in modeles)
            {
                foreach (var taille in Tailles)
                {
                    var reference = $"{prefixe}-{numero:000}";
                    var rupture = aleatoire.Next(100) < 8;
                    var stock = new Dictionary<string, int>
                    {
                        ["ROU"] = rupture ? 0 : aleatoire.Next(0, 900),
                        ["LES"] = rupture ? 0 : aleatoire.Next(0, 600)
                    };

                    articles.Add(new Article(reference, $"{libelle} - taille {taille}", famille, prix, stock));
                    numero++;
                }
            }
        }

        return articles;
    }
}
