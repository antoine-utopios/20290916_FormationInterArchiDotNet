namespace Textinord.Trame.Domain;

/// <summary>
/// Racine d'agrégat : la commande et ses lignes. Toute modification passe par ses méthodes ;
/// la collection de lignes n'est exposée qu'en lecture.
/// </summary>
public sealed class Commande
{
    public const decimal PlafondRemisePourcent = 30m;
    public const decimal RemiseVolumePourcent = 5m;
    public const int SeuilRemiseVolume = 500;

    private readonly List<LigneCommande> _lignes = [];

    public int Id { get; private set; }
    public string Numero { get; private set; }
    public int ClientId { get; private set; }
    public Client? Client { get; private set; }
    public DateTime Date { get; private set; }
    public StatutCommande Statut { get; private set; }
    public MetadonneesCommande Metadonnees { get; private set; }

    /// <summary>Jeton de concurrence optimiste : renouvelé à chaque sauvegarde par le DbContext.</summary>
    public Guid Version { get; private set; }

    public IReadOnlyCollection<LigneCommande> Lignes => _lignes.AsReadOnly();

    private Commande()
    {
        Numero = string.Empty;
        Metadonnees = new MetadonneesCommande("ADV");
    }

    public Commande(string numero, Client client, DateTime date, MetadonneesCommande? metadonnees = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (!NumeroCommande.EstValide(numero))
        {
            throw new RegleMetierException($"Numéro de commande invalide : '{numero}' (attendu CMD-AAAA-NNNNNN).");
        }

        Numero = numero;
        Client = client;
        ClientId = client.Id;
        Date = date;
        Statut = StatutCommande.Brouillon;
        Metadonnees = metadonnees ?? new MetadonneesCommande("ADV");
        Version = Guid.NewGuid();
    }

    public decimal TotalHt => _lignes.Sum(l => l.MontantHt);

    /// <summary>
    /// Ajoute une ligne en appliquant la règle de remise Textinord :
    /// remise client (0 à 25 %) + 5 % de remise volume au-delà de 500 pièces, plafond 30 %.
    /// </summary>
    public LigneCommande AjouterLigne(Article article, int quantite, decimal? prixNegocie = null)
    {
        ArgumentNullException.ThrowIfNull(article);
        ExigerStatut(StatutCommande.Brouillon, "ajouter une ligne");

        var remiseClient = Client?.RemisePourcent ?? 0m;
        var remiseVolume = quantite > SeuilRemiseVolume ? RemiseVolumePourcent : 0m;
        var remise = Math.Min(remiseClient + remiseVolume, PlafondRemisePourcent);

        var ligne = new LigneCommande(article, quantite, prixNegocie ?? article.PrixBase, remise);
        _lignes.Add(ligne);
        return ligne;
    }

    /// <summary>
    /// Valide la commande si chaque ligne dispose d'un stock dans au moins un entrepôt ;
    /// sinon elle passe en attente de stock.
    /// </summary>
    public void Valider(Func<LigneCommande, bool> stockDisponible)
    {
        ArgumentNullException.ThrowIfNull(stockDisponible);
        if (Statut is not (StatutCommande.Brouillon or StatutCommande.EnAttenteStock))
        {
            throw new RegleMetierException($"Impossible de valider une commande au statut {Statut}.");
        }

        if (_lignes.Count == 0)
        {
            throw new RegleMetierException("Une commande sans ligne ne peut pas être validée.");
        }

        Statut = _lignes.All(stockDisponible) ? StatutCommande.Validee : StatutCommande.EnAttenteStock;
    }

    public void DemarrerPreparation()
    {
        ExigerStatut(StatutCommande.Validee, "démarrer la préparation");
        Statut = StatutCommande.EnPreparation;
    }

    public void Expedier()
    {
        ExigerStatut(StatutCommande.EnPreparation, "expédier");
        Statut = StatutCommande.Expediee;
    }

    public void Facturer()
    {
        ExigerStatut(StatutCommande.Expediee, "facturer");
        Statut = StatutCommande.Facturee;
    }

    /// <summary>Règle Textinord : annulation possible tant que la préparation n'a pas commencé.</summary>
    public void Annuler(string motif)
    {
        if (Statut is StatutCommande.EnPreparation or StatutCommande.Expediee or StatutCommande.Facturee)
        {
            throw new RegleMetierException($"Une commande {Statut} ne peut plus être annulée.");
        }

        Statut = StatutCommande.Annulee;
        Metadonnees.Commenter($"Annulée : {motif}");
    }

    private void ExigerStatut(StatutCommande attendu, string action)
    {
        if (Statut != attendu)
        {
            throw new RegleMetierException($"Impossible de {action} : statut {Statut}, attendu {attendu}.");
        }
    }
}
