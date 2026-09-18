namespace Textinord.Trame.Application.Commandes.Messages;

/// <summary>Command Message : une intention, adressée à un seul destinataire, qui peut échouer.</summary>
public sealed record ValiderCommande(string Numero);
