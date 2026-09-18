namespace Textinord.Trame.Domain;

/// <summary>Client professionnel : la condition tarifaire donne le taux de remise (0 à 25 %).</summary>
public sealed class Client
{
    public required string Code { get; init; }

    public required string RaisonSociale { get; init; }

    public decimal TauxRemise { get; init; }

    public bool Actif { get; init; } = true;
}
