namespace Textinord.Trame.Domain;

/// <summary>
/// Repository spécifique à l'agrégat Commande (pattern Repository, Fowler / Evans).
/// Il ne sauvegarde rien : c'est l'unité de travail qui décide du moment du commit.
/// </summary>
public interface ICommandeRepository
{
    Task<Commande?> ObtenirAsync(int id, CancellationToken ct = default);

    Task<Commande?> ObtenirParNumeroAsync(string numero, CancellationToken ct = default);

    /// <summary>Prochain numéro CMD-AAAA-NNNNNN pour l'année donnée.</summary>
    Task<string> ProchainNumeroAsync(int annee, CancellationToken ct = default);

    /// <summary>Lecture sans suivi (no-tracking) : listes, écrans, exports.</summary>
    Task<IReadOnlyList<Commande>> ListerParStatutAsync(StatutCommande statut, CancellationToken ct = default);

    void Ajouter(Commande commande);
}
