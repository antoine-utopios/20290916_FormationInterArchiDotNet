namespace Textinord.Extranet.Modeles;

/// <summary>
/// Règle de remise du fil rouge : remise client (0 à 25 %) + remise volume (+5 %
/// au-delà de 500 pièces sur une ligne), le tout plafonné à 30 %.
/// </summary>
public static class Tarification
{
    public const int SeuilVolume = 500;
    public const decimal RemiseVolumePourcent = 5m;
    public const decimal PlafondPourcent = 30m;

    public static decimal CalculerTauxRemise(decimal remiseClientPourcent, int quantite)
    {
        if (remiseClientPourcent is < 0 or > 25)
        {
            throw new ArgumentOutOfRangeException(nameof(remiseClientPourcent), "La remise client est comprise entre 0 et 25 %.");
        }

        var taux = remiseClientPourcent;
        if (quantite > SeuilVolume)
        {
            taux += RemiseVolumePourcent;
        }

        return Math.Min(taux, PlafondPourcent);
    }
}
