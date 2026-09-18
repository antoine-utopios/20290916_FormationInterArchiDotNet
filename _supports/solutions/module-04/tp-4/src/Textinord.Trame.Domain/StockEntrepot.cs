namespace Textinord.Trame.Domain;

/// <summary>Quantité disponible d'un article dans un entrepôt (Roubaix : RBX, Lesquin : LSQ).</summary>
public sealed class StockEntrepot
{
    public string CodeEntrepot { get; private set; }
    public int Quantite { get; private set; }

    private StockEntrepot()
    {
        CodeEntrepot = string.Empty;
    }

    public StockEntrepot(string codeEntrepot, int quantite)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeEntrepot);
        ArgumentOutOfRangeException.ThrowIfNegative(quantite);
        CodeEntrepot = codeEntrepot.Trim().ToUpperInvariant();
        Quantite = quantite;
    }

    internal void Ajuster(int nouvelleQuantite)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(nouvelleQuantite);
        Quantite = nouvelleQuantite;
    }
}
