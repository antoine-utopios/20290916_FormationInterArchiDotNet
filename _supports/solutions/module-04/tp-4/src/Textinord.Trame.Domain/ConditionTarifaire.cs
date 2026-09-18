namespace Textinord.Trame.Domain;

/// <summary>Condition tarifaire négociée avec un client : elle fixe la remise client (0 à 25 %).</summary>
public enum ConditionTarifaire
{
    Standard,
    Collectivite,
    GrandCompte
}

public static class ConditionTarifaireExtensions
{
    /// <summary>Remise client en pourcentage, entre 0 et 25 (règle Textinord).</summary>
    public static decimal RemisePourcent(this ConditionTarifaire condition) => condition switch
    {
        ConditionTarifaire.Standard => 0m,
        ConditionTarifaire.Collectivite => 12m,
        ConditionTarifaire.GrandCompte => 25m,
        _ => throw new ArgumentOutOfRangeException(nameof(condition))
    };
}
