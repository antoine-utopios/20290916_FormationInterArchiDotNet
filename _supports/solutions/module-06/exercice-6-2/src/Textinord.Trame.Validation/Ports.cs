namespace Textinord.Trame.Validation;

/// <summary>Référentiel clients (table SQL, cache Redis… peu importe ici : c'est un port).</summary>
public interface IReferentielClients
{
    Task<Client?> TrouverAsync(string code, CancellationToken ct = default);
}

/// <summary>Stock par entrepôt : renvoie les codes d'entrepôt capables de servir la quantité, par ordre de préférence.</summary>
public interface IStockDisponible
{
    Task<IReadOnlyList<string>> EntrepotsDisponiblesAsync(string reference, int quantite, CancellationToken ct = default);
}

/// <summary>Alerte l'administration des ventes (mail Teams, ticket…) d'une rupture bloquant une commande.</summary>
public interface INotificateurAdv
{
    Task SignalerRuptureAsync(string numeroCommande, string reference, CancellationToken ct = default);
}
