namespace Textinord.Trame.Domain;

public sealed class LigneCommande
{
    // Constructeur réservé à EF Core.
    private LigneCommande()
    {
    }

    public LigneCommande(string referenceArticle, int quantite, decimal prixUnitaire, decimal tauxRemiseClient)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceArticle);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantite);
        ArgumentOutOfRangeException.ThrowIfNegative(prixUnitaire);

        ReferenceArticle = referenceArticle;
        Quantite = quantite;
        PrixUnitaire = prixUnitaire;
        TauxRemise = RegleRemise.Calculer(tauxRemiseClient, quantite);
    }

    public int Id { get; private set; }

    public Guid CommandeId { get; private set; }

    public string ReferenceArticle { get; private set; } = null!;

    public int Quantite { get; private set; }

    public decimal PrixUnitaire { get; private set; }

    /// <summary>Taux effectivement appliqué : client + volume, plafonné (voir <see cref="RegleRemise"/>).</summary>
    public decimal TauxRemise { get; private set; }

    /// <summary>Entrepôt qui servira la ligne, affecté à la validation.</summary>
    public string? EntrepotAffecte { get; private set; }

    public decimal MontantNet =>
        Math.Round(Quantite * PrixUnitaire * (1 - TauxRemise), 2, MidpointRounding.AwayFromZero);

    internal void AffecterEntrepot(string entrepot) => EntrepotAffecte = entrepot;
}
