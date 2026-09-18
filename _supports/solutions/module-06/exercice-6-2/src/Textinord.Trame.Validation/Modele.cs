namespace Textinord.Trame.Validation;

public enum StatutCommande
{
    Brouillon,
    Validee,
    EnAttenteStock,
    Refusee
}

public sealed record Client(string Code, string RaisonSociale, decimal TauxRemise, bool Actif);

public sealed class LigneCommande(string reference, int quantite, decimal prixUnitaire)
{
    public string Reference { get; } = reference;

    public int Quantite { get; } = quantite;

    public decimal PrixUnitaire { get; } = prixUnitaire;

    public decimal TauxRemise { get; internal set; }

    public string? Entrepot { get; internal set; }

    public decimal MontantNet => Math.Round(Quantite * PrixUnitaire * (1 - TauxRemise), 2, MidpointRounding.AwayFromZero);
}

public sealed class Commande(string numero, string codeClient)
{
    public string Numero { get; } = numero;

    public string CodeClient { get; } = codeClient;

    public List<LigneCommande> Lignes { get; } = [];

    public StatutCommande Statut { get; internal set; } = StatutCommande.Brouillon;

    public DateTimeOffset? ValideeLe { get; internal set; }
}

public sealed record ResultatValidation(StatutCommande Statut, IReadOnlyList<string> Motifs)
{
    public bool EstValidee => Statut == StatutCommande.Validee;
}
