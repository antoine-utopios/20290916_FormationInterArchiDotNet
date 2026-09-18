namespace Textinord.Trame.Domain;

/// <summary>Les deux entrepôts de Textinord.</summary>
public static class Entrepots
{
    public const string Roubaix = "RBX";
    public const string Lesquin = "LSQ";
}

/// <summary>Stock disponible d'un article dans un entrepôt.</summary>
public sealed class StockArticle
{
    public required string Reference { get; init; }

    public required string Entrepot { get; init; }

    public int Quantite { get; set; }
}
