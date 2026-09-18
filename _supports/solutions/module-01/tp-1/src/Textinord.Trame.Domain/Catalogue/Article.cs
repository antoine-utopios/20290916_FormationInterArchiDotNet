namespace Textinord.Trame.Domain.Catalogue;

/// <summary>
/// Une référence du catalogue (40 000 chez Textinord) avec son stock par entrepôt.
/// </summary>
public sealed class Article
{
    private readonly Dictionary<Entrepot, int> _stocks = [];

    public Article(string reference, string libelle, string famille, decimal prixBase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(libelle);
        ArgumentException.ThrowIfNullOrWhiteSpace(famille);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(prixBase);

        Reference = reference;
        Libelle = libelle;
        Famille = famille;
        PrixBase = prixBase;
    }

    public string Reference { get; }

    public string Libelle { get; }

    public string Famille { get; }

    public decimal PrixBase { get; }

    public IReadOnlyDictionary<Entrepot, int> StockParEntrepot => _stocks;

    /// <summary>Fixe le stock d'un entrepôt. Retourne l'article pour chaîner les appels (style Builder).</summary>
    public Article AvecStock(Entrepot entrepot, int quantite)
    {
        ArgumentNullException.ThrowIfNull(entrepot);
        ArgumentOutOfRangeException.ThrowIfNegative(quantite);
        _stocks[entrepot] = quantite;
        return this;
    }

    public int StockDisponible(Entrepot entrepot) => _stocks.GetValueOrDefault(entrepot);

    /// <summary>Le premier entrepôt, par priorité, capable de servir la quantité demandée ; null sinon.</summary>
    public Entrepot? EntrepotPouvantServir(int quantite) =>
        _stocks.Where(s => s.Value >= quantite)
               .Select(s => s.Key)
               .OrderBy(e => e.Priorite)
               .FirstOrDefault();

    public override string ToString() => $"{Reference} — {Libelle}";
}
