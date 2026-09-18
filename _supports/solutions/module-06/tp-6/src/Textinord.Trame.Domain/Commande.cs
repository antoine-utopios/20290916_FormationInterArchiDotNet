using Textinord.Trame.Domain.Events;

namespace Textinord.Trame.Domain;

public sealed class Commande
{
    private readonly List<LigneCommande> _lignes = [];

    // Constructeur réservé à EF Core.
    private Commande()
    {
    }

    public Guid Id { get; private set; }

    public string Numero { get; private set; } = null!;

    public string CodeClient { get; private set; } = null!;

    public DateTimeOffset CreeeLe { get; private set; }

    public DateTimeOffset? ValideeLe { get; private set; }

    public StatutCommande Statut { get; private set; }

    public IReadOnlyList<LigneCommande> Lignes => _lignes;

    public decimal MontantNet => _lignes.Sum(l => l.MontantNet);

    public static Resultat<Commande> Creer(
        string numero, string codeClient, DateTimeOffset creeeLe, IEnumerable<LigneCommande> lignes)
    {
        var erreurs = new List<string>();
        var liste = lignes.ToList();

        if (!NumeroCommande.EstValide(numero))
        {
            erreurs.Add($"Numéro de commande invalide : {numero}.");
        }

        if (string.IsNullOrWhiteSpace(codeClient))
        {
            erreurs.Add("Le code client est obligatoire.");
        }

        if (liste.Count == 0)
        {
            erreurs.Add("Une commande doit contenir au moins une ligne.");
        }

        if (erreurs.Count > 0)
        {
            return Resultat<Commande>.Echec([.. erreurs]);
        }

        var commande = new Commande
        {
            Id = Guid.NewGuid(),
            Numero = numero,
            CodeClient = codeClient,
            CreeeLe = creeeLe,
            Statut = StatutCommande.Brouillon,
        };
        commande._lignes.AddRange(liste);
        return Resultat<Commande>.Succes(commande);
    }

    /// <summary>
    /// Règle Textinord : une commande se valide seulement si chaque ligne a un stock
    /// disponible dans au moins un entrepôt ; sinon elle passe en EnAttenteStock.
    /// </summary>
    /// <param name="entrepotParReference">Pour chaque référence, l'entrepôt qui peut servir la ligne, ou null en rupture.</param>
    /// <returns>L'événement à publier, ou null si la commande attend du stock.</returns>
    public CommandeValidee? Valider(IReadOnlyDictionary<string, string?> entrepotParReference, DateTimeOffset validationLe)
    {
        if (Statut is not (StatutCommande.Brouillon or StatutCommande.EnAttenteStock))
        {
            throw new InvalidOperationException(
                $"La commande {Numero} est au statut {Statut} : elle ne peut plus être validée.");
        }

        var enRupture = _lignes
            .Where(l => !entrepotParReference.TryGetValue(l.ReferenceArticle, out var entrepot) || entrepot is null)
            .ToList();

        if (enRupture.Count > 0)
        {
            Statut = StatutCommande.EnAttenteStock;
            return null;
        }

        foreach (var ligne in _lignes)
        {
            ligne.AffecterEntrepot(entrepotParReference[ligne.ReferenceArticle]!);
        }

        Statut = StatutCommande.Validee;
        ValideeLe = validationLe;

        return new CommandeValidee(
            Id,
            Numero,
            CodeClient,
            validationLe,
            _lignes.Select(l => new LigneAPreparer(l.ReferenceArticle, l.Quantite, l.EntrepotAffecte!)).ToList());
    }

    public void DemarrerPreparation()
    {
        if (Statut != StatutCommande.Validee)
        {
            throw new InvalidOperationException(
                $"La commande {Numero} est au statut {Statut} : la préparation ne peut pas démarrer.");
        }

        Statut = StatutCommande.EnPreparation;
    }

    /// <summary>Annulation possible avant préparation uniquement.</summary>
    public void Annuler()
    {
        if (Statut is StatutCommande.EnPreparation or StatutCommande.Expediee or StatutCommande.Facturee)
        {
            throw new InvalidOperationException(
                $"La commande {Numero} est {Statut} : annulation impossible après le début de la préparation.");
        }

        Statut = StatutCommande.Annulee;
    }
}
