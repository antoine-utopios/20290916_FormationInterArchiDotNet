namespace Textinord.Trame.Validation;

/// <summary>Remise client (0 à 25 %) + 5 % au-delà de 500 pièces, plafond 30 %.</summary>
public static class RegleRemise
{
    public static decimal Calculer(decimal tauxClient, int quantite)
    {
        if (tauxClient is < 0 or > 0.25m)
        {
            throw new ArgumentOutOfRangeException(nameof(tauxClient), tauxClient, "Taux client entre 0 et 25 %.");
        }

        var taux = tauxClient + (quantite > 500 ? 0.05m : 0m);
        return Math.Min(taux, 0.30m);
    }
}
