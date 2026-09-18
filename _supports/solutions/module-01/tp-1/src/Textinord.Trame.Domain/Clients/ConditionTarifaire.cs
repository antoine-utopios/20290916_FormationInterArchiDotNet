namespace Textinord.Trame.Domain.Clients;

/// <summary>
/// Condition tarifaire négociée avec un client : remise de 0 à 25 % (règle Textinord).
/// Objet-valeur : deux conditions de même code et même taux sont égales.
/// </summary>
public sealed record ConditionTarifaire
{
    public const decimal TauxMaximal = 0.25m;

    public ConditionTarifaire(string code, decimal tauxRemise)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        if (tauxRemise is < 0m or > TauxMaximal)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tauxRemise), tauxRemise, "La remise client est comprise entre 0 et 25 %.");
        }

        Code = code;
        TauxRemise = tauxRemise;
    }

    public string Code { get; }

    public decimal TauxRemise { get; }

    public static readonly ConditionTarifaire Standard = new("STD", 0.00m);

    public static readonly ConditionTarifaire Collectivite = new("COL", 0.10m);

    public static readonly ConditionTarifaire Hotellerie = new("HOT", 0.12m);

    public static readonly ConditionTarifaire Industriel = new("IND", 0.15m);

    public static readonly ConditionTarifaire GrandCompte = new("GC", 0.25m);
}
