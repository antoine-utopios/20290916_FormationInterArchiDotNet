using System.Collections.Frozen;

namespace Textinord.Trame.Api.Domaine;

/// <summary>
/// Agrégat Commande : les règles du cycle de vie vivent ici, dans un seul endroit testable.
/// Dans Trame 2, ce type appartient à Textinord.Trame.Domain (TP 1) ; il est recopié, allégé,
/// pour que le TP 5 se construise sans dépendre des TP précédents.
/// </summary>
public sealed class Commande
{
    /// <summary>La machine à états, écrite noir sur blanc : ce qui n'est pas listé est interdit.</summary>
    private static readonly FrozenDictionary<StatutCommande, StatutCommande[]> Transitions =
        new Dictionary<StatutCommande, StatutCommande[]>
        {
            [StatutCommande.Brouillon] = [StatutCommande.Validee, StatutCommande.EnAttenteStock, StatutCommande.Annulee],
            [StatutCommande.EnAttenteStock] = [StatutCommande.Validee, StatutCommande.Annulee],
            [StatutCommande.Validee] = [StatutCommande.EnPreparation, StatutCommande.Annulee],
            [StatutCommande.EnPreparation] = [StatutCommande.Expediee],
            [StatutCommande.Expediee] = [StatutCommande.Facturee],
            [StatutCommande.Facturee] = [],
            [StatutCommande.Annulee] = [],
        }.ToFrozenDictionary();

    private readonly List<LigneCommande> _lignes = [];
    private readonly Lock _verrou = new();

    public Commande(string numero, string codeClient, decimal tauxRemiseClient, DateOnly date)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(numero);
        ArgumentException.ThrowIfNullOrWhiteSpace(codeClient);

        Numero = numero;
        CodeClient = codeClient;
        TauxRemiseClient = tauxRemiseClient;
        Date = date;
    }

    /// <summary>Numéro au format CMD-AAAA-NNNNNN, unique et séquentiel par année.</summary>
    public string Numero { get; }

    public string CodeClient { get; }

    public decimal TauxRemiseClient { get; }

    public DateOnly Date { get; }

    public StatutCommande Statut { get; private set; } = StatutCommande.Brouillon;

    public IReadOnlyList<LigneCommande> Lignes => _lignes;

    public int NombrePieces => _lignes.Sum(l => l.Quantite);

    public decimal TotalBrut => _lignes.Sum(l => l.MontantBrut);

    public decimal TotalRemises => _lignes.Sum(l => l.MontantRemise);

    public decimal TotalHT => _lignes.Sum(l => l.MontantNet);

    public bool EstModifiable => Statut is StatutCommande.Brouillon or StatutCommande.EnAttenteStock;

    public LigneCommande AjouterLigne(string reference, int quantite, decimal prixUnitaire)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantite);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(prixUnitaire);

        lock (_verrou)
        {
            if (!EstModifiable)
            {
                throw new InvalidOperationException($"La commande {Numero} est {Statut} : ses lignes ne changent plus.");
            }

            var remise = CalculRemise.Calculer(TauxRemiseClient, quantite);
            var ligne = new LigneCommande(_lignes.Count + 1, reference, quantite, prixUnitaire, remise);
            _lignes.Add(ligne);
            return ligne;
        }
    }

    /// <summary>
    /// Valide la commande : si chaque ligne a du stock dans au moins un entrepôt, elle passe Validee
    /// et l'entrepôt qui servira chaque ligne est affecté ; sinon elle passe EnAttenteStock.
    /// </summary>
    /// <param name="entrepotPouvantServir">Retourne le code de l'entrepôt capable de servir (référence, quantité), ou null.</param>
    public Result Valider(Func<string, int, string?> entrepotPouvantServir)
    {
        ArgumentNullException.ThrowIfNull(entrepotPouvantServir);

        lock (_verrou)
        {
            if (_lignes.Count == 0)
            {
                return Result.Echec("COMMANDE_VIDE", "Une commande sans ligne ne se valide pas.");
            }

            if (!PeutPasserA(StatutCommande.Validee))
            {
                return TransitionInterdite(StatutCommande.Validee);
            }

            var affectations = _lignes
                .Select(l => (Ligne: l, Entrepot: entrepotPouvantServir(l.Reference, l.Quantite)))
                .ToList();

            if (affectations.Any(a => a.Entrepot is null))
            {
                Statut = StatutCommande.EnAttenteStock;
                return Result.Ok();
            }

            foreach (var (ligne, entrepot) in affectations)
            {
                ligne.EntrepotAffecte = entrepot;
            }

            Statut = StatutCommande.Validee;
            return Result.Ok();
        }
    }

    /// <summary>Annulation possible tant que la préparation n'a pas commencé.</summary>
    public Result Annuler()
    {
        lock (_verrou)
        {
            if (!PeutPasserA(StatutCommande.Annulee))
            {
                return TransitionInterdite(StatutCommande.Annulee);
            }

            Statut = StatutCommande.Annulee;
            return Result.Ok();
        }
    }

    public bool PeutPasserA(StatutCommande cible) => Transitions[Statut].Contains(cible);

    /// <summary>Les transitions autorisées depuis un statut donné (exposées par le référentiel de l'API).</summary>
    public static IReadOnlyList<StatutCommande> TransitionsDepuis(StatutCommande statut) => Transitions[statut];

    private Result TransitionInterdite(StatutCommande cible) =>
        Result.Echec("TRANSITION_INTERDITE", $"Passage {Statut} → {cible} interdit pour la commande {Numero}.");
}
