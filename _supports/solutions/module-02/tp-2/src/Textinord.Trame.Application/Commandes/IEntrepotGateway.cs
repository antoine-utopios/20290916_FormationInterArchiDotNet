using Textinord.Trame.Application.Commandes.Messages;
using Textinord.Trame.Domain.Entrepots;

namespace Textinord.Trame.Application.Commandes;

/// <summary>
/// Agent métier vers le système d'entrepôt (Channel Adapter côté sortie).
/// L'implémentation réelle parlera au WMS ; celle du TP journalise.
/// </summary>
public interface IEntrepotGateway
{
    Task TransmettreAsync(Entrepot entrepot, OrdrePreparationEntrepot ordre, CancellationToken cancellationToken);
}
