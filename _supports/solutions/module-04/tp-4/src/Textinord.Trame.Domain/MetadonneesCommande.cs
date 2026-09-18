namespace Textinord.Trame.Domain;

/// <summary>
/// Informations de contexte d'une commande, sans structure relationnelle stable :
/// origine (extranet, EDI, ADV), référence côté client, commentaire, étiquettes.
/// Persistées dans une colonne JSON (owned type, voir CommandeConfiguration).
/// </summary>
public sealed class MetadonneesCommande
{
    public string Origine { get; private set; }
    public string? ReferenceClient { get; private set; }
    public string? Commentaire { get; private set; }
    public List<string> Etiquettes { get; private set; }

    private MetadonneesCommande()
    {
        Origine = "ADV";
        Etiquettes = [];
    }

    public MetadonneesCommande(string origine, string? referenceClient = null, string? commentaire = null,
        IEnumerable<string>? etiquettes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(origine);
        Origine = origine;
        ReferenceClient = referenceClient;
        Commentaire = commentaire;
        Etiquettes = etiquettes?.ToList() ?? [];
    }

    public void Commenter(string commentaire) => Commentaire = commentaire;

    public void Etiqueter(string etiquette)
    {
        if (!Etiquettes.Contains(etiquette, StringComparer.OrdinalIgnoreCase))
        {
            Etiquettes.Add(etiquette);
        }
    }
}
