namespace Textinord.Trame.Domain.Tarification;

/// <summary>
/// Le détail d'une remise appliquée à une ligne : on garde les composantes pour pouvoir
/// l'expliquer au client (et la tester), pas seulement le taux final.
/// </summary>
public sealed record Remise(decimal TauxClient, decimal TauxVolume, decimal TauxApplique)
{
    /// <summary>Vrai quand le plafond de 30 % a réduit le cumul.</summary>
    public bool Plafonnee => TauxClient + TauxVolume > TauxApplique;

    public static readonly Remise Aucune = new(0m, 0m, 0m);
}
