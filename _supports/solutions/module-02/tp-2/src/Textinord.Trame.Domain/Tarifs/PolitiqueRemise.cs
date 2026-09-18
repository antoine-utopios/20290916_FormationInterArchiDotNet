namespace Textinord.Trame.Domain.Tarifs;

/// <summary>
/// Règle métier Textinord : remise client (0-25 %) + remise volume (+5 % au-delà de 500 pièces
/// sur une ligne), cumul plafonné à 30 %.
/// </summary>
public static class PolitiqueRemise
{
    public const decimal PlafondPourcent = 30m;

    public const int SeuilVolumePieces = 500;

    public const decimal RemiseVolumePourcent = 5m;

    public static decimal Calculer(ConditionTarifaire condition, int quantite)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantite);

        var remise = condition.RemisePourcent;

        if (quantite > SeuilVolumePieces)
        {
            remise += RemiseVolumePourcent;
        }

        return Math.Min(remise, PlafondPourcent);
    }
}
