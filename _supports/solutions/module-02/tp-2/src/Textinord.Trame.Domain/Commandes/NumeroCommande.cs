using System.Globalization;
using System.Text.RegularExpressions;

namespace Textinord.Trame.Domain.Commandes;

/// <summary>Numéro de commande au format CMD-AAAA-NNNNNN, unique et séquentiel par année.</summary>
public readonly partial record struct NumeroCommande
{
    public NumeroCommande(string valeur)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valeur);

        if (!Format().IsMatch(valeur))
        {
            throw new ArgumentException(
                $"Numéro de commande invalide : « {valeur} » (attendu CMD-AAAA-NNNNNN).",
                nameof(valeur));
        }

        Valeur = valeur;
    }

    public string Valeur { get; }

    public int Annee => int.Parse(Valeur.AsSpan(4, 4), CultureInfo.InvariantCulture);

    public static NumeroCommande Generer(int annee, int sequence)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(annee, 2000);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sequence, 999_999);

        return new NumeroCommande(string.Create(
            CultureInfo.InvariantCulture,
            $"CMD-{annee:D4}-{sequence:D6}"));
    }

    public override string ToString() => Valeur;

    [GeneratedRegex(@"^CMD-\d{4}-\d{6}$")]
    private static partial Regex Format();
}
