namespace Textinord.Trame.Application.Commandes.Messages;

/// <summary>Résultat du splitter : la part d'une commande validée qui concerne un entrepôt.</summary>
public sealed record CommandeParEntrepot(CommandeValidee Commande, string EntrepotCode);
