using Textinord.Trame.Domain.Tarifs;

namespace Textinord.Trame.Domain.Commandes;

/// <summary>Une ligne de commande : un article, une quantité, un prix négocié, un entrepôt de prélèvement.</summary>
public sealed class LigneCommande
{
    public LigneCommande(string reference, int quantite, decimal prixUnitaire, decimal remisePourcent, string entrepotCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(entrepotCode);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantite);
        ArgumentOutOfRangeException.ThrowIfNegative(prixUnitaire);
        ArgumentOutOfRangeException.ThrowIfNegative(remisePourcent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(remisePourcent, PolitiqueRemise.PlafondPourcent);

        Reference = reference;
        Quantite = quantite;
        PrixUnitaire = prixUnitaire;
        RemisePourcent = remisePourcent;
        EntrepotCode = entrepotCode;
    }

    public string Reference { get; }

    public int Quantite { get; }

    public decimal PrixUnitaire { get; }

    public decimal RemisePourcent { get; }

    public string EntrepotCode { get; }

    public decimal MontantNet =>
        Math.Round(Quantite * PrixUnitaire * (1 - (RemisePourcent / 100m)), 2, MidpointRounding.AwayFromZero);
}
