namespace Textinord.Trame.Domain;

/// <summary>
/// Cycle de vie d'une commande Textinord :
/// Brouillon → Validee → EnPreparation → Expediee → Facturee.
/// Une commande sans stock disponible passe en EnAttenteStock ;
/// l'annulation n'est possible qu'avant la préparation.
/// </summary>
public enum StatutCommande
{
    Brouillon,
    EnAttenteStock,
    Validee,
    EnPreparation,
    Expediee,
    Facturee,
    Annulee
}
