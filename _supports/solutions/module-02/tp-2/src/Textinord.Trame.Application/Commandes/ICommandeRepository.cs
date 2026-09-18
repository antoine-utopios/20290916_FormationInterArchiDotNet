using Textinord.Trame.Domain.Commandes;

namespace Textinord.Trame.Application.Commandes;

public interface ICommandeRepository
{
    Task<Commande?> TrouverAsync(NumeroCommande numero, CancellationToken cancellationToken);

    Task<IReadOnlyList<Commande>> ListerAsync(CancellationToken cancellationToken);

    Task EnregistrerAsync(Commande commande, CancellationToken cancellationToken);
}
