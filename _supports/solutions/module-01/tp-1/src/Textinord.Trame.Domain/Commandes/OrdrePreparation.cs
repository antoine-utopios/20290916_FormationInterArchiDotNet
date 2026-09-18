using Textinord.Trame.Domain.Catalogue;
using Textinord.Trame.Domain.Communs;

namespace Textinord.Trame.Domain.Commandes;

public enum StatutOrdrePreparation
{
    Emis,
    EnCours,
    Clos,
}

public sealed record LigneAPreparer(int NumeroLigne, string ReferenceArticle, int Quantite);

/// <summary>
/// Ordre envoyé à un entrepôt pour préparer les lignes d'une commande qui lui sont affectées.
/// Un ordre par entrepôt concerné, émis à la validation (règle Textinord).
/// </summary>
public sealed class OrdrePreparation
{
    internal OrdrePreparation(NumeroCommande commande, Entrepot entrepot, IReadOnlyList<LigneAPreparer> lignes)
    {
        if (lignes.Count == 0)
        {
            throw new ArgumentException("Un ordre de préparation contient au moins une ligne.", nameof(lignes));
        }

        Commande = commande;
        Entrepot = entrepot;
        Lignes = lignes;
    }

    public NumeroCommande Commande { get; }

    public Entrepot Entrepot { get; }

    public IReadOnlyList<LigneAPreparer> Lignes { get; }

    public string? Preparateur { get; private set; }

    public StatutOrdrePreparation Statut { get; private set; } = StatutOrdrePreparation.Emis;

    public Result Affecter(string preparateur)
    {
        if (string.IsNullOrWhiteSpace(preparateur))
        {
            return Result.Echec("PREPARATEUR_VIDE", "Le nom du préparateur est obligatoire.");
        }

        if (Statut != StatutOrdrePreparation.Emis)
        {
            return Result.Echec("ORDRE_DEJA_PRIS", $"L'ordre {Commande}/{Entrepot.Code} est déjà {Statut}.");
        }

        Preparateur = preparateur;
        Statut = StatutOrdrePreparation.EnCours;
        return Result.Ok();
    }

    public Result Clore()
    {
        if (Statut != StatutOrdrePreparation.EnCours)
        {
            return Result.Echec("ORDRE_NON_EN_COURS", $"Impossible de clore un ordre {Statut} : il doit être en cours.");
        }

        Statut = StatutOrdrePreparation.Clos;
        return Result.Ok();
    }

    public override string ToString() => $"OP {Commande} / {Entrepot.Code} — {Lignes.Count} ligne(s), {Statut}";
}
