namespace Textinord.Extranet.Modeles;

public enum StatutCommande
{
    Brouillon,
    Validee,
    EnAttenteStock,
    EnPreparation,
    Expediee,
    Facturee
}

public sealed record LigneCommande(string Reference, string Libelle, int Quantite, decimal PrixUnitaire, decimal TauxRemise, decimal MontantNet);

/// <summary>Commande enregistrée : numéro CMD-AAAA-NNNNNN, séquentiel par année.</summary>
public sealed record Commande(
    string Numero,
    DateTime Date,
    string CodeClient,
    string ReferenceClient,
    DateOnly DateLivraisonSouhaitee,
    IReadOnlyList<LigneCommande> Lignes,
    decimal TotalNet,
    StatutCommande Statut);
