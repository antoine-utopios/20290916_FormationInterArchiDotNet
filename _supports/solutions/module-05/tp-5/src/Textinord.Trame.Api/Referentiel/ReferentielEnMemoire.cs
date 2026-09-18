using System.Collections.Frozen;

namespace Textinord.Trame.Api.Referentiel;

/// <summary>
/// Catalogue en mémoire : un extrait de six références sur les 40 000 du vrai catalogue.
/// Enregistré en singleton : les données sont immuables et partagées par toutes les requêtes.
/// Le module 4 remplace cette classe par un DbContext EF Core sur Azure SQL.
/// </summary>
public sealed class ReferentielArticlesEnMemoire : IReferentielArticles
{
    private static readonly FrozenDictionary<string, Article> Articles = new[]
    {
        Creer("VT-PARKA-XL", "Parka haute visibilité classe 3, taille XL", "Vêtements de travail", 64.90m, rbx: 1_200, lsq: 300),
        Creer("VT-PANT-44", "Pantalon multipoches, taille 44", "Vêtements de travail", 29.50m, rbx: 2_400, lsq: 800),
        Creer("EPI-GANT-09", "Gants anti-coupure niveau D, taille 9", "EPI textiles", 7.80m, rbx: 0, lsq: 0),
        Creer("EPI-GILET-L", "Gilet de signalisation, taille L", "EPI textiles", 4.20m, rbx: 5_000, lsq: 5_000),
        Creer("HOT-DRAP-160", "Drap plat percale 160 x 290", "Linge hôtelier", 18.40m, rbx: 0, lsq: 950),
        Creer("HOT-SERV-50", "Serviette éponge 50 x 100, 500 g", "Linge hôtelier", 5.60m, rbx: 3_000, lsq: 1_200),
    }.ToFrozenDictionary(a => a.Reference, StringComparer.OrdinalIgnoreCase);

    public Article? Trouver(string reference) =>
        Articles.TryGetValue(reference, out var article) ? article : null;

    public IReadOnlyCollection<Article> Tous() => Articles.Values;

    private static Article Creer(string reference, string libelle, string famille, decimal prix, int rbx, int lsq) =>
        new(reference, libelle, famille, prix, new Dictionary<string, int> { ["RBX"] = rbx, ["LSQ"] = lsq });
}

/// <summary>Quatre clients représentatifs des 1 200 clients professionnels de Textinord.</summary>
public sealed class ReferentielClientsEnMemoire : IReferentielClients
{
    private static readonly FrozenDictionary<string, Client> Clients = new Client[]
    {
        new("CLI-0042", "Ville de Roubaix — services techniques", "COL", 0.10m),
        new("CLI-0107", "Hôtel Grand Nord, Lille", "HOT", 0.12m),
        new("CLI-0311", "Aciéries de Denain", "IND", 0.15m),
        new("CLI-0500", "Groupe hospitalier de la Métropole", "GC", 0.25m),
    }.ToFrozenDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

    public Client? Trouver(string code) =>
        Clients.TryGetValue(code, out var client) ? client : null;
}
