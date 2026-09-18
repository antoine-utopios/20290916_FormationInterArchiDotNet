using Textinord.Trame.Domain.Clients;

namespace Textinord.Trame.Domain.Tarification;

/// <summary>
/// Règle Textinord : remise client (0 à 25 % selon la condition tarifaire)
/// + remise volume de 5 % au-delà de 500 pièces sur une ligne ; cumul plafonné à 30 %.
/// Fonction pure : aucune dépendance, testable en isolation.
/// </summary>
public static class CalculRemise
{
    /// <summary>La remise volume s'applique à partir de la 501e pièce.</summary>
    public const int SeuilVolume = 500;

    public const decimal TauxVolume = 0.05m;

    public const decimal Plafond = 0.30m;

    public static Remise Calculer(decimal tauxClient, int quantite)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tauxClient);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantite);

        var tauxVolume = quantite > SeuilVolume ? TauxVolume : 0m;
        var tauxApplique = Math.Min(tauxClient + tauxVolume, Plafond);

        return new Remise(tauxClient, tauxVolume, tauxApplique);
    }

    public static Remise Calculer(ConditionTarifaire condition, int quantite)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return Calculer(condition.TauxRemise, quantite);
    }
}
