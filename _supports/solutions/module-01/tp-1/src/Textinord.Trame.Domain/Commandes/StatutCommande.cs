namespace Textinord.Trame.Domain.Commandes;

/// <summary>
/// Cycle de vie d'une commande Textinord :
/// Brouillon → Validee → EnPreparation → Expediee → Facturee ;
/// EnAttenteStock quand une ligne n'a de stock dans aucun entrepôt ;
/// Annulee possible tant que la préparation n'a pas commencé.
/// </summary>
public enum StatutCommande
{
    Brouillon,
    EnAttenteStock,
    Validee,
    EnPreparation,
    Expediee,
    Facturee,
    Annulee,
}
