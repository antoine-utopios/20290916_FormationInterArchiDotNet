namespace Textinord.Trame.Domain.Commandes;

/// <summary>Agrégat Commande : lignes, statut et transitions autorisées.</summary>
public sealed class Commande
{
    private readonly List<LigneCommande> _lignes = [];

    public Commande(NumeroCommande numero, string clientCode, string paysLivraison, DateTimeOffset creeeLe, bool urgente = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(paysLivraison);

        Numero = numero;
        ClientCode = clientCode;
        PaysLivraison = paysLivraison.ToUpperInvariant();
        CreeeLe = creeeLe;
        Urgente = urgente;
    }

    public NumeroCommande Numero { get; }

    public string ClientCode { get; }

    /// <summary>Code pays ISO 3166-1 alpha-2 (FR, BE, DE…).</summary>
    public string PaysLivraison { get; }

    public DateTimeOffset CreeeLe { get; }

    public bool Urgente { get; }

    public StatutCommande Statut { get; private set; } = StatutCommande.Brouillon;

    public IReadOnlyList<LigneCommande> Lignes => _lignes;

    public bool EstExport => !string.Equals(PaysLivraison, "FR", StringComparison.Ordinal);

    public decimal MontantNet => _lignes.Sum(ligne => ligne.MontantNet);

    public IReadOnlyList<string> EntrepotsConcernes =>
        _lignes.Select(ligne => ligne.EntrepotCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public void AjouterLigne(LigneCommande ligne)
    {
        ArgumentNullException.ThrowIfNull(ligne);
        ExigerStatut(StatutCommande.Brouillon, "ajouter une ligne");
        _lignes.Add(ligne);
    }

    /// <summary>
    /// Règle métier : une commande se valide seulement si chaque ligne a un stock disponible
    /// dans au moins un entrepôt ; sinon elle passe en attente de stock.
    /// </summary>
    public void Valider(IDisponibiliteStock stock)
    {
        ArgumentNullException.ThrowIfNull(stock);
        ExigerStatut(StatutCommande.Brouillon, "valider");

        if (_lignes.Count == 0)
        {
            throw new InvalidOperationException($"La commande {Numero} n'a aucune ligne : validation impossible.");
        }

        var toutDisponible = _lignes.All(ligne => stock.EstDisponible(ligne.Reference, ligne.Quantite));
        Statut = toutDisponible ? StatutCommande.Validee : StatutCommande.EnAttenteStock;
    }

    public void DemarrerPreparation()
    {
        ExigerStatut(StatutCommande.Validee, "démarrer la préparation");
        Statut = StatutCommande.EnPreparation;
    }

    public void DeclarerExpediee()
    {
        ExigerStatut(StatutCommande.EnPreparation, "déclarer l'expédition");
        Statut = StatutCommande.Expediee;
    }

    /// <summary>Règle métier : annulation possible tant que la préparation n'a pas démarré.</summary>
    public void Annuler()
    {
        if (Statut is StatutCommande.EnPreparation or StatutCommande.Expediee or StatutCommande.Facturee)
        {
            throw new InvalidOperationException(
                $"La commande {Numero} est {Statut} : annulation impossible après le début de la préparation.");
        }

        Statut = StatutCommande.Annulee;
    }

    private void ExigerStatut(StatutCommande attendu, string action)
    {
        if (Statut != attendu)
        {
            throw new InvalidOperationException(
                $"Impossible de {action} : la commande {Numero} est {Statut}, attendu {attendu}.");
        }
    }
}
