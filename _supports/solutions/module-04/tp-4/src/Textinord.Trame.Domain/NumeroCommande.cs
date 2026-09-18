using System.Text.RegularExpressions;

namespace Textinord.Trame.Domain;

/// <summary>Numéro de commande Textinord : CMD-AAAA-NNNNNN, unique et séquentiel par année.</summary>
public static partial class NumeroCommande
{
    public const string Prefixe = "CMD-";

    [GeneratedRegex(@"^CMD-\d{4}-\d{6}$")]
    private static partial Regex Format();

    public static bool EstValide(string numero) => !string.IsNullOrEmpty(numero) && Format().IsMatch(numero);

    public static string Former(int annee, int sequence) => $"{Prefixe}{annee:D4}-{sequence:D6}";

    /// <summary>Extrait le rang séquentiel d'un numéro (000042 → 42).</summary>
    public static int Sequence(string numero) =>
        EstValide(numero) ? int.Parse(numero[^6..]) : throw new RegleMetierException($"Numéro invalide : {numero}");
}
