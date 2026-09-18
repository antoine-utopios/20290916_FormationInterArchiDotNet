using System.Collections.Concurrent;
using Textinord.Extranet.Modeles;

namespace Textinord.Extranet.Services;

/// <summary>
/// Carnet de commandes en mémoire. Applique deux règles du fil rouge : numérotation
/// CMD-AAAA-NNNNNN séquentielle par année, et passage en EnAttenteStock si une ligne
/// n'a de stock suffisant dans aucun entrepôt.
/// </summary>
public sealed class CommandeEnMemoire : ICommandeService
{
    private readonly ConcurrentDictionary<string, Commande> commandes = new();
    private readonly ConcurrentDictionary<int, int> compteursParAnnee = new();
    private readonly TimeProvider horloge;

    public CommandeEnMemoire() : this(TimeProvider.System)
    {
    }

    public CommandeEnMemoire(TimeProvider horloge)
    {
        this.horloge = horloge;
    }

    public Task<Commande> PasserAsync(CommandeSaisie saisie, Client client, IReadOnlyList<LignePanier> lignes, CancellationToken ct = default)
    {
        if (lignes.Count == 0)
        {
            throw new InvalidOperationException("Impossible de passer une commande vide.");
        }

        var maintenant = horloge.GetLocalNow().DateTime;
        var sequence = compteursParAnnee.AddOrUpdate(maintenant.Year, 1, (_, valeur) => valeur + 1);
        var numero = $"CMD-{maintenant.Year}-{sequence:000000}";

        var lignesCommande = lignes
            .Select(l => new LigneCommande(l.Article.Reference, l.Article.Libelle, l.Quantite, l.PrixUnitaire, l.TauxRemise, l.MontantNet))
            .ToList();

        var statut = lignes.All(l => l.Article.EstDisponible(l.Quantite))
            ? StatutCommande.Validee
            : StatutCommande.EnAttenteStock;

        var commande = new Commande(
            numero,
            maintenant,
            client.Code,
            saisie.ReferenceClient ?? string.Empty,
            saisie.DateLivraisonSouhaitee ?? DateOnly.FromDateTime(maintenant).AddDays(2),
            lignesCommande,
            lignesCommande.Sum(l => l.MontantNet),
            statut);

        commandes[numero] = commande;
        return Task.FromResult(commande);
    }

    public Task<Commande?> TrouverAsync(string numero, CancellationToken ct = default) =>
        Task.FromResult(commandes.GetValueOrDefault(numero));
}
