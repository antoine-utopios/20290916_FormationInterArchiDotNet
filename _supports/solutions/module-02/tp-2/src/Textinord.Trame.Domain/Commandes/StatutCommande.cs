namespace Textinord.Trame.Domain.Commandes;

/// <summary>Cycle de vie d'une commande Textinord (voir FIL-ROUGE.md).</summary>
public enum StatutCommande
{
    Brouillon,
    Validee,
    EnAttenteStock,
    EnPreparation,
    Expediee,
    Facturee,
    Annulee,
}
