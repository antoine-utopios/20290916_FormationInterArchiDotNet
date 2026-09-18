namespace Textinord.Trame.Domain;

/// <summary>Référence du catalogue Textinord (40 000 références, stock par entrepôt).</summary>
public sealed class Article
{
    private readonly List<StockEntrepot> _stocks = [];

    public int Id { get; private set; }
    public string Reference { get; private set; }
    public string Libelle { get; private set; }
    public string Famille { get; private set; }
    public decimal PrixBase { get; private set; }
    public IReadOnlyCollection<StockEntrepot> Stocks => _stocks.AsReadOnly();

    private Article()
    {
        Reference = string.Empty;
        Libelle = string.Empty;
        Famille = string.Empty;
    }

    public Article(string reference, string libelle, string famille, decimal prixBase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(libelle);
        ArgumentException.ThrowIfNullOrWhiteSpace(famille);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(prixBase);
        Reference = reference.Trim().ToUpperInvariant();
        Libelle = libelle.Trim();
        Famille = famille.Trim();
        PrixBase = prixBase;
    }

    /// <summary>Fixe le stock d'un entrepôt (création ou mise à jour).</summary>
    public void DefinirStock(string codeEntrepot, int quantite)
    {
        var code = codeEntrepot.Trim().ToUpperInvariant();
        var existant = _stocks.FirstOrDefault(s => s.CodeEntrepot == code);
        if (existant is null)
        {
            _stocks.Add(new StockEntrepot(code, quantite));
        }
        else
        {
            existant.Ajuster(quantite);
        }
    }

    /// <summary>Règle Textinord : une ligne est servable si au moins un entrepôt a la quantité demandée.</summary>
    public bool EstDisponible(int quantite) => _stocks.Any(s => s.Quantite >= quantite);

    public int StockTotal => _stocks.Sum(s => s.Quantite);
}
