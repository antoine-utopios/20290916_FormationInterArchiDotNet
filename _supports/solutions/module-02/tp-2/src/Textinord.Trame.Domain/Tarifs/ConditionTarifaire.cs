namespace Textinord.Trame.Domain.Tarifs;

/// <summary>Condition tarifaire négociée avec un client : remise de 0 à 25 %.</summary>
public sealed record ConditionTarifaire
{
    public const decimal RemiseMaximalePourcent = 25m;

    public ConditionTarifaire(string code, decimal remisePourcent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentOutOfRangeException.ThrowIfNegative(remisePourcent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(remisePourcent, RemiseMaximalePourcent);

        Code = code;
        RemisePourcent = remisePourcent;
    }

    public string Code { get; }

    public decimal RemisePourcent { get; }

    public static ConditionTarifaire Standard { get; } = new("STD", 0m);

    public static ConditionTarifaire GrandCompte { get; } = new("GC", 18m);
}
