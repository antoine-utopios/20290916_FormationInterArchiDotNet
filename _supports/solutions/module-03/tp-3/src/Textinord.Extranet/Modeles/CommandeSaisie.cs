using System.ComponentModel.DataAnnotations;

namespace Textinord.Extranet.Modeles;

/// <summary>Formulaire de passage de commande, validé par DataAnnotations.</summary>
public sealed class CommandeSaisie : IValidatableObject
{
    [Required(ErrorMessage = "Votre référence de commande est obligatoire.")]
    [StringLength(30, ErrorMessage = "30 caractères maximum.")]
    public string? ReferenceClient { get; set; }

    [Required(ErrorMessage = "L'adresse de livraison est obligatoire.")]
    [StringLength(120, ErrorMessage = "120 caractères maximum.")]
    public string? AdresseLivraison { get; set; }

    [Required(ErrorMessage = "Le code postal est obligatoire.")]
    [RegularExpression(@"^\d{5}$", ErrorMessage = "Le code postal comporte cinq chiffres.")]
    public string? CodePostal { get; set; }

    [Required(ErrorMessage = "La ville est obligatoire.")]
    [StringLength(60, ErrorMessage = "60 caractères maximum.")]
    public string? Ville { get; set; }

    [Required(ErrorMessage = "Indiquez une date de livraison souhaitée.")]
    public DateOnly? DateLivraisonSouhaitee { get; set; }

    [StringLength(500, ErrorMessage = "500 caractères maximum.")]
    public string? Commentaire { get; set; }

    [Range(typeof(bool), "true", "true", ErrorMessage = "Vous devez accepter les conditions générales de vente.")]
    public bool AccepteCgv { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var minimum = DateOnly.FromDateTime(DateTime.Today).AddDays(2);
        if (DateLivraisonSouhaitee is { } date && date < minimum)
        {
            yield return new ValidationResult(
                $"Textinord livre au plus tôt le {minimum:dd/MM/yyyy} (48 h de préparation).",
                [nameof(DateLivraisonSouhaitee)]);
        }
    }
}
