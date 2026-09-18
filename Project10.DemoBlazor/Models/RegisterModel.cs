using System.ComponentModel.DataAnnotations;

namespace Project10.DemoBlazor.Models;

public class RegisterModel
{
    [Required (ErrorMessage = "Champ Email requis !"), EmailAddress( ErrorMessage = "Ceci n'est pas un email valide !")] public string Email { get; set; } = string.Empty;
    [Required (ErrorMessage = "Champ Password requis !"), MinLength(6, ErrorMessage = "Le mot de passe doit faire au moins 6 caractères !")] public string Password { get; set; } = string.Empty;
}