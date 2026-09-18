namespace Textinord.Trame.Api.Commandes;

public sealed record NouvelleCommandeRequete(string CodeClient, IReadOnlyList<LigneRequete> Lignes);

public sealed record LigneRequete(string Reference, int Quantite);

public sealed record CommandeReponse(
    string Numero,
    string CodeClient,
    string Statut,
    decimal MontantNet,
    DateTimeOffset CreeeLe,
    DateTimeOffset? ValideeLe,
    IReadOnlyList<LigneReponse> Lignes);

public sealed record LigneReponse(
    string Reference,
    int Quantite,
    decimal PrixUnitaire,
    decimal TauxRemise,
    decimal MontantNet,
    string? Entrepot);

public sealed record OrdrePreparationReponse(
    Guid Id,
    string Entrepot,
    string Statut,
    int NombreLignes,
    int NombrePieces,
    DateTimeOffset EmisLe,
    string? Preparateur);

public sealed record OutboxReponse(
    long Id,
    Guid MessageId,
    string Type,
    DateTimeOffset CreeLe,
    DateTimeOffset? EnvoyeLe,
    int Tentatives,
    string? DerniereErreur);
