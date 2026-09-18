using System.Collections.Concurrent;
using Textinord.Trame.Application.Commandes;
using Textinord.Trame.Domain.Commandes;

namespace Textinord.Trame.Infrastructure.Persistance;

/// <summary>Dépôt en mémoire, pré-rempli : EF Core prend sa place au module 4.</summary>
public sealed class InMemoryCommandeRepository : ICommandeRepository
{
    private readonly ConcurrentDictionary<string, Commande> _commandes = new(StringComparer.Ordinal);

    public InMemoryCommandeRepository(TimeProvider horloge)
    {
        ArgumentNullException.ThrowIfNull(horloge);

        foreach (var commande in JeuDeDemonstration.Creer(horloge.GetUtcNow()))
        {
            _commandes[commande.Numero.Valeur] = commande;
        }
    }

    public Task<Commande?> TrouverAsync(NumeroCommande numero, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_commandes.GetValueOrDefault(numero.Valeur));
    }

    public Task<IReadOnlyList<Commande>> ListerAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<Commande> liste = _commandes.Values.OrderBy(c => c.Numero.Valeur, StringComparer.Ordinal).ToList();
        return Task.FromResult(liste);
    }

    public Task EnregistrerAsync(Commande commande, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commande);
        cancellationToken.ThrowIfCancellationRequested();
        _commandes[commande.Numero.Valeur] = commande;
        return Task.CompletedTask;
    }
}
