namespace Textinord.Extranet.Modeles;

/// <summary>Article du catalogue Textinord, tel qu'exposé à l'extranet.</summary>
public sealed record Article(
    string Reference,
    string Libelle,
    string Famille,
    decimal PrixBase,
    IReadOnlyDictionary<string, int> StockParEntrepot)
{
    public int StockTotal => StockParEntrepot.Values.Sum();

    public bool EstDisponible(int quantite) =>
        StockParEntrepot.Values.Any(stock => stock >= quantite);
}
