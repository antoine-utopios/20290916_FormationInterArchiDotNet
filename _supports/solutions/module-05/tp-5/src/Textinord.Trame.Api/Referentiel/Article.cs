namespace Textinord.Trame.Api.Referentiel;

/// <summary>Un article du catalogue Textinord avec son stock par entrepôt (Roubaix : RBX, Lesquin : LSQ).</summary>
public sealed record Article(string Reference, string Libelle, string Famille, decimal PrixBase, IReadOnlyDictionary<string, int> StockParEntrepot)
{
    /// <summary>Premier entrepôt capable de servir la quantité demandée, Roubaix d'abord, sinon null.</summary>
    public string? EntrepotPouvantServir(int quantite) =>
        StockParEntrepot
            .Where(kv => kv.Value >= quantite)
            .OrderBy(kv => kv.Key == "RBX" ? 0 : 1)
            .Select(kv => kv.Key)
            .FirstOrDefault();
}

/// <summary>Un client professionnel et sa condition tarifaire (remise de 0 à 25 %).</summary>
public sealed record Client(string Code, string RaisonSociale, string ConditionTarifaire, decimal TauxRemise);

public interface IReferentielArticles
{
    Article? Trouver(string reference);

    IReadOnlyCollection<Article> Tous();
}

public interface IReferentielClients
{
    Client? Trouver(string code);
}
