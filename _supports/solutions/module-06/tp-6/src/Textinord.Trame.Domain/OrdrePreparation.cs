using Textinord.Trame.Domain.Events;

namespace Textinord.Trame.Domain;

public enum StatutOrdre
{
    EnAttente,
    EnCours,
    Clos
}

/// <summary>
/// Ordre de préparation : un par entrepôt concerné, émis dès la validation de la commande.
/// Le couple (commande, entrepôt) est unique : c'est la clé d'idempotence du consumer.
/// </summary>
public sealed class OrdrePreparation
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid CommandeId { get; init; }

    public required string NumeroCommande { get; init; }

    public required string Entrepot { get; init; }

    public int NombreLignes { get; init; }

    public int NombrePieces { get; init; }

    public DateTimeOffset EmisLe { get; init; }

    public StatutOrdre Statut { get; private set; } = StatutOrdre.EnAttente;

    public string? Preparateur { get; private set; }

    /// <summary>Un ordre pour un entrepôt, à partir des lignes de la commande validée qui le concernent.</summary>
    public static OrdrePreparation Depuis(
        CommandeValidee commande, IGrouping<string, LigneAPreparer> lignesEntrepot, DateTimeOffset emisLe) => new()
    {
        CommandeId = commande.CommandeId,
        NumeroCommande = commande.Numero,
        Entrepot = lignesEntrepot.Key,
        NombreLignes = lignesEntrepot.Count(),
        NombrePieces = lignesEntrepot.Sum(l => l.Quantite),
        EmisLe = emisLe,
    };

    public void Affecter(string preparateur)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preparateur);
        Preparateur = preparateur;
        Statut = StatutOrdre.EnCours;
    }

    public void Clore() => Statut = StatutOrdre.Clos;
}
