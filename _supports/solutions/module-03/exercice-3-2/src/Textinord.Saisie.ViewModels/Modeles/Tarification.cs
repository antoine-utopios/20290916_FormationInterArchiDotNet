namespace Textinord.Saisie.ViewModels.Modeles;

/// <summary>
/// Règle de remise du fil rouge (dans Trame 2, elle vit dans Textinord.Trame.Domain,
/// module 1) : remise client 0-25 %, +5 % au-delà de 500 pièces, plafond 30 %.
/// </summary>
public static class Tarification
{
    public const int SeuilVolume = 500;
    public const decimal RemiseVolumePourcent = 5m;
    public const decimal PlafondPourcent = 30m;

    public static decimal CalculerTauxRemise(decimal remiseClientPourcent, int quantite)
    {
        var taux = remiseClientPourcent + (quantite > SeuilVolume ? RemiseVolumePourcent : 0m);
        return Math.Min(taux, PlafondPourcent);
    }
}
