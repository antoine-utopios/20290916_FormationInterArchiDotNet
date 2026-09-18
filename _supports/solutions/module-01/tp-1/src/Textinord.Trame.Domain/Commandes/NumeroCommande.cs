using System.Globalization;
using System.Text.RegularExpressions;
using Textinord.Trame.Domain.Communs;

namespace Textinord.Trame.Domain.Commandes;

/// <summary>
/// Numéro de commande au format CMD-AAAA-NNNNNN, unique et séquentiel par année.
/// Objet-valeur : le format est garanti à la construction, plus jamais revérifié ailleurs.
/// </summary>
public sealed partial record NumeroCommande
{
    public NumeroCommande(int annee, int sequence)
    {
        if (annee is < 2011 or > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(annee), annee, "Année hors du domaine de Trame (2011-2100).");
        }

        if (sequence is < 1 or > 999_999)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "La séquence est comprise entre 1 et 999999.");
        }

        Annee = annee;
        Sequence = sequence;
    }

    public int Annee { get; }

    public int Sequence { get; }

    public string Valeur => $"CMD-{Annee:D4}-{Sequence:D6}";

    public static Result<NumeroCommande> Parser(string? texte)
    {
        if (string.IsNullOrWhiteSpace(texte))
        {
            return Result<NumeroCommande>.Echec("NUMERO_VIDE", "Le numéro de commande est vide.");
        }

        var correspondance = FormatNumero().Match(texte);
        if (!correspondance.Success)
        {
            return Result<NumeroCommande>.Echec("NUMERO_INVALIDE", $"« {texte} » ne respecte pas le format CMD-AAAA-NNNNNN.");
        }

        var annee = int.Parse(correspondance.Groups[1].Value, CultureInfo.InvariantCulture);
        var sequence = int.Parse(correspondance.Groups[2].Value, CultureInfo.InvariantCulture);

        if (sequence == 0)
        {
            return Result<NumeroCommande>.Echec("NUMERO_INVALIDE", "La séquence commence à 000001.");
        }

        return Result<NumeroCommande>.Ok(new NumeroCommande(annee, sequence));
    }

    public override string ToString() => Valeur;

    [GeneratedRegex(@"^CMD-(\d{4})-(\d{6})$")]
    private static partial Regex FormatNumero();
}
