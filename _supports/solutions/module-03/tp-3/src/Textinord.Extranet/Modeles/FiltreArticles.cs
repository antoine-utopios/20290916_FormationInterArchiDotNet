using System.ComponentModel.DataAnnotations;

namespace Textinord.Extranet.Modeles;

/// <summary>Critères du formulaire de filtre du catalogue.</summary>
public sealed class FiltreArticles
{
    [StringLength(40, ErrorMessage = "La recherche est limitée à 40 caractères.")]
    public string? Texte { get; set; }

    public string? Famille { get; set; }

    public bool EnStockSeulement { get; set; }

    public FiltreArticles Copier() => new()
    {
        Texte = Texte,
        Famille = Famille,
        EnStockSeulement = EnStockSeulement
    };
}
