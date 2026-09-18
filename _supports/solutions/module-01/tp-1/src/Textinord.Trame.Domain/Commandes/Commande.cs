using System.Collections.Frozen;
using Textinord.Trame.Domain.Catalogue;
using Textinord.Trame.Domain.Clients;
using Textinord.Trame.Domain.Commandes.Specifications;
using Textinord.Trame.Domain.Communs;
using Textinord.Trame.Domain.Tarification;

namespace Textinord.Trame.Domain.Commandes;

/// <summary>
/// Agrégat Commande : racine de cohérence pour ses lignes et ses ordres de préparation.
/// Toutes les règles du cycle de vie vivent ici, dans un seul endroit testable, et nulle part
/// dans un trigger SQL ou un code-behind WinForms.
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
    private readonly List<OrdrePreparation> _ordres = [];

    public Commande(NumeroCommande numero, Client client, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(numero);
        ArgumentNullException.ThrowIfNull(client);

        Numero = numero;
        Client = client;
        Date = date;
    }

    public NumeroCommande Numero { get; }

    public Client Client { get; }

    public DateOnly Date { get; }

    public StatutCommande Statut { get; private set; } = StatutCommande.Brouillon;

    public IReadOnlyList<LigneCommande> Lignes => _lignes;

    public IReadOnlyList<OrdrePreparation> OrdresPreparation => _ordres;

    public int NombrePieces => _lignes.Sum(l => l.Quantite);

    public decimal TotalHT => _lignes.Sum(l => l.MontantNet);

    public bool EstModifiable => Statut is StatutCommande.Brouillon or StatutCommande.EnAttenteStock;

    public Result<LigneCommande> AjouterLigne(Article article, int quantite, decimal? prixNegocie = null)
    {
        ArgumentNullException.ThrowIfNull(article);

        if (!EstModifiable)
        {
            return Result<LigneCommande>.Echec("COMMANDE_FIGEE", $"La commande {Numero} est {Statut} : ses lignes ne changent plus.");
        }

        if (quantite <= 0)
        {
            return Result<LigneCommande>.Echec("QUANTITE_INVALIDE", "La quantité d'une ligne est strictement positive.");
        }

        if (prixNegocie is <= 0m)
        {
            return Result<LigneCommande>.Echec("PRIX_INVALIDE", "Le prix négocié est strictement positif.");
        }

        var remise = CalculRemise.Calculer(Client.ConditionTarifaire, quantite);
        var ligne = new LigneCommande(_lignes.Count + 1, article, quantite, prixNegocie ?? article.PrixBase, remise);
        _lignes.Add(ligne);

        return Result<LigneCommande>.Ok(ligne);
    }

    /// <summary>
    /// Valide la commande : si chaque ligne a du stock quelque part, elle passe Validee et un ordre
    /// de préparation est émis par entrepôt concerné ; sinon elle passe EnAttenteStock.
    /// </summary>
    public Result<StatutCommande> Valider()
    {
        if (_lignes.Count == 0)
        {
            return Result<StatutCommande>.Echec("COMMANDE_VIDE", "Une commande sans ligne ne se valide pas.");
        }

        var regle = new CommandeValidableSpecification();
        if (!regle.EstSatisfaitePar(this))
        {
            var transition = Transiter(StatutCommande.EnAttenteStock);
            return transition.EstSucces
                ? Result<StatutCommande>.Ok(Statut)
                : Result<StatutCommande>.Echec(transition.Erreur!);
        }

        var passage = Transiter(StatutCommande.Validee);
        if (passage.EstEchec)
        {
            return Result<StatutCommande>.Echec(passage.Erreur!);
        }

        EmettreOrdresPreparation();
        return Result<StatutCommande>.Ok(Statut);
    }

    public Result DemarrerPreparation() => Transiter(StatutCommande.EnPreparation);

    /// <summary>L'expédition est déclarée quand tous les ordres de préparation sont clos.</summary>
    public Result Expedier()
    {
        if (Statut != StatutCommande.EnPreparation)
        {
            return Transiter(StatutCommande.Expediee);
        }

        var ordresOuverts = _ordres.Count(o => o.Statut != StatutOrdrePreparation.Clos);
        if (ordresOuverts > 0)
        {
            return Result.Echec("ORDRES_OUVERTS", $"{ordresOuverts} ordre(s) de préparation encore ouvert(s) sur {Numero}.");
        }

        return Transiter(StatutCommande.Expediee);
    }

    public Result Facturer() => Transiter(StatutCommande.Facturee);

    /// <summary>Annulation possible tant que la préparation n'a pas commencé.</summary>
    public Result Annuler() => Transiter(StatutCommande.Annulee);

    public bool PeutPasserA(StatutCommande cible) => Transitions[Statut].Contains(cible);

    private Result Transiter(StatutCommande cible)
    {
        if (!PeutPasserA(cible))
        {
            return Result.Echec("TRANSITION_INTERDITE", $"Passage {Statut} → {cible} interdit pour la commande {Numero}.");
        }

        Statut = cible;
        return Result.Ok();
    }

    private void EmettreOrdresPreparation()
    {
        _ordres.Clear();

        foreach (var ligne in _lignes)
        {
            ligne.EntrepotAffecte = ligne.Article.EntrepotPouvantServir(ligne.Quantite);
        }

        var parEntrepot = _lignes
            .Where(l => l.EntrepotAffecte is not null)
            .GroupBy(l => l.EntrepotAffecte!)
            .OrderBy(g => g.Key.Priorite);

        foreach (var groupe in parEntrepot)
        {
            var lignes = groupe
                .Select(l => new LigneAPreparer(l.Numero, l.Article.Reference, l.Quantite))
                .ToList();

            _ordres.Add(new OrdrePreparation(Numero, groupe.Key, lignes));
        }
    }

    public override string ToString() => $"{Numero} — {Client.RaisonSociale} — {Statut} — {TotalHT:0.00} € HT";
}
