namespace Textinord.Trame.Domain;

/// <summary>
/// Cycle de vie d'une commande Textinord :
/// Brouillon → Validee → EnPreparation → Expediee → Facturee.
/// Une commande dont une ligne n'a pas de stock passe en EnAttenteStock ;
/// l'annulation n'est possible qu'avant le début de la préparation.
/// </summary>
public enum StatutCommande
{
    Brouillon,
    Validee,
    EnAttenteStock,
    EnPreparation,
    Expediee,
    Facturee,
    Annulee
}
