namespace Textinord.Trame.Domain.Events;

/// <summary>
/// Événement émis à la validation d'une commande. Il transite par l'Outbox puis par le bus
/// (topic « commandes ») ; les entrepôts s'y abonnent pour créer leurs ordres de préparation.
/// Contrat sérialisé en JSON : ne renommez pas les membres sans versionner le message.
/// </summary>
public sealed record CommandeValidee(
    Guid CommandeId,
    string Numero,
    string CodeClient,
    DateTimeOffset ValideeLe,
    IReadOnlyList<LigneAPreparer> Lignes);

public sealed record LigneAPreparer(string Reference, int Quantite, string Entrepot);
