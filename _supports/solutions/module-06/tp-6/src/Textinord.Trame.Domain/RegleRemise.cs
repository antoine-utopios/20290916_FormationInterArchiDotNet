namespace Textinord.Trame.Domain;

/// <summary>
/// Règle de remise Textinord : remise client (0 à 25 % selon la condition tarifaire),
/// remise volume de 5 % au-delà de 500 pièces sur une ligne ; les deux se cumulent
/// avec un plafond de 30 %.
/// </summary>
public static class RegleRemise
{
    public const decimal TauxClientMaximum = 0.25m;
    public const int SeuilVolume = 500;
    public const decimal RemiseVolume = 0.05m;
    public const decimal Plafond = 0.30m;

    public static decimal Calculer(decimal tauxClient, int quantite)
    {
        if (tauxClient is < 0 or > TauxClientMaximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tauxClient), tauxClient,
                "Le taux de remise client doit être compris entre 0 et 25 %.");
        }

        var taux = tauxClient + (quantite > SeuilVolume ? RemiseVolume : 0m);
        return Math.Min(taux, Plafond);
    }
}
