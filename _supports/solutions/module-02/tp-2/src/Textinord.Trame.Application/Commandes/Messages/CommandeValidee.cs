namespace Textinord.Trame.Application.Commandes.Messages;

/// <summary>Event Message : un fait passé, au format canonique de Trame 2.</summary>
public sealed record CommandeValidee(
    string Numero,
    string ClientCode,
    string? PaysLivraison,
    bool Urgente,
    IReadOnlyList<LigneValidee> Lignes)
{
    public bool EstExport => !string.Equals(PaysLivraison, "FR", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<string> EntrepotsConcernes =>
        Lignes.Select(ligne => ligne.EntrepotCode.ToUpperInvariant()).Distinct(StringComparer.Ordinal).ToList();
}

public sealed record LigneValidee(string Reference, int Quantite, string EntrepotCode);
