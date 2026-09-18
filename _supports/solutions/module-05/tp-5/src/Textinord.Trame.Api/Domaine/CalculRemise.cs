namespace Textinord.Trame.Api.Domaine;

/// <summary>
/// Le détail d'une remise appliquée à une ligne : on garde les composantes pour pouvoir
/// l'expliquer au client dans la version 2 de l'API, pas seulement le taux final.
/// </summary>
public sealed record Remise(decimal TauxClient, decimal TauxVolume, decimal TauxApplique)
{
    /// <summary>Vrai quand le cumul client + volume atteint le plafond de 30 % (grand compte avec remise volume).</summary>
    public bool PlafondAtteint => TauxClient + TauxVolume >= CalculRemise.Plafond;
}

/// <summary>
/// Règle Textinord : remise client (0 à 25 % selon la condition tarifaire)
/// + remise volume de 5 % au-delà de 500 pièces sur une ligne ; cumul plafonné à 30 %.
/// Fonction pure, recopiée du noyau métier du TP 1 pour que ce TP soit autonome.
/// </summary>
public static class CalculRemise
{
    public const int SeuilVolume = 500;

    public const decimal TauxVolume = 0.05m;

    public const decimal Plafond = 0.30m;

    public const decimal TauxClientMaximal = 0.25m;

    public static Remise Calculer(decimal tauxClient, int quantite)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tauxClient);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(tauxClient, TauxClientMaximal);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantite);

        var tauxVolume = quantite > SeuilVolume ? TauxVolume : 0m;
        var tauxApplique = Math.Min(tauxClient + tauxVolume, Plafond);

        return new Remise(tauxClient, tauxVolume, tauxApplique);
    }
}
