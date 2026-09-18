using System.Globalization;

namespace Textinord.Extranet.Services;

/// <summary>Formats d'affichage de l'extranet, indépendants de la culture du serveur.</summary>
public static class Format
{
    private static readonly CultureInfo FrFr = CultureInfo.GetCultureInfo("fr-FR");

    public static string Prix(decimal montant) => string.Format(FrFr, "{0:N2} €", montant);

    public static string Pourcent(decimal taux) => string.Format(FrFr, "{0:N0} %", taux);

    public static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", FrFr);
}
