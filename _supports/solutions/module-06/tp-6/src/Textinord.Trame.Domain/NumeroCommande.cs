using System.Text.RegularExpressions;

namespace Textinord.Trame.Domain;

/// <summary>Numéro de commande : CMD-AAAA-NNNNNN, unique, séquentiel par année.</summary>
public static partial class NumeroCommande
{
    public static string Formater(int annee, int sequence) => $"CMD-{annee:D4}-{sequence:D6}";

    public static bool EstValide(string numero) => Format().IsMatch(numero);

    [GeneratedRegex(@"^CMD-\d{4}-\d{6}$")]
    private static partial Regex Format();
}
