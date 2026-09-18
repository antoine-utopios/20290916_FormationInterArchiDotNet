using System.Collections.Concurrent;
using Textinord.Trame.Api.Domaine;

namespace Textinord.Trame.Api.Commandes;

/// <summary>Port de persistance des commandes. Implémentation mémoire ici, EF Core au module 4.</summary>
public interface ICommandesStore
{
    Commande? Trouver(string numero);

    void Ajouter(Commande commande);

    IReadOnlyList<Commande> Rechercher(StatutCommande? statut, string? codeClient, int page, int taille, out int total);
}

/// <summary>Attribue les numéros CMD-AAAA-NNNNNN, séquentiels par année.</summary>
public interface IGenerateurNumero
{
    string Suivant(DateOnly date);
}

/// <summary>
/// Stockage en mémoire, singleton : un ConcurrentDictionary suffit tant que l'API tourne sur une
/// seule instance. Dès que Container Apps monte à deux réplicas, ce stockage devient faux :
/// c'est précisément la raison d'être du module 4 (Azure SQL) et de l'état hors du processus.
/// </summary>
public sealed class CommandesEnMemoire : ICommandesStore
{
    private readonly ConcurrentDictionary<string, Commande> _commandes = new(StringComparer.OrdinalIgnoreCase);

    public Commande? Trouver(string numero) =>
        _commandes.TryGetValue(numero, out var commande) ? commande : null;

    public void Ajouter(Commande commande)
    {
        ArgumentNullException.ThrowIfNull(commande);

        if (!_commandes.TryAdd(commande.Numero, commande))
        {
            throw new InvalidOperationException($"Le numéro {commande.Numero} est déjà attribué.");
        }
    }

    public IReadOnlyList<Commande> Rechercher(StatutCommande? statut, string? codeClient, int page, int taille, out int total)
    {
        var requete = _commandes.Values.AsEnumerable();

        if (statut is not null)
        {
            requete = requete.Where(c => c.Statut == statut);
        }

        if (!string.IsNullOrWhiteSpace(codeClient))
        {
            requete = requete.Where(c => string.Equals(c.CodeClient, codeClient, StringComparison.OrdinalIgnoreCase));
        }

        var filtrees = requete.OrderByDescending(c => c.Numero).ToList();
        total = filtrees.Count;

        return filtrees.Skip((page - 1) * taille).Take(taille).ToList();
    }
}

/// <summary>
/// Compteur par année, en mémoire. Le compteur de Trame (table SQL) sera repris au module 4 ;
/// ici on démarre à 4 512 pour que les numéros ressemblent à ceux de la production.
/// </summary>
public sealed class GenerateurNumeroEnMemoire : IGenerateurNumero
{
    private readonly ConcurrentDictionary<int, int> _compteurs = new();

    public string Suivant(DateOnly date)
    {
        var sequence = _compteurs.AddOrUpdate(date.Year, _ => 4_512, (_, actuel) => actuel + 1);
        return $"CMD-{date.Year:D4}-{sequence:D6}";
    }
}
