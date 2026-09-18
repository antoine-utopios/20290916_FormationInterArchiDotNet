using System.Collections.Concurrent;
using Textinord.Trame.Application.Commandes;
using Textinord.Trame.Application.Commandes.Messages;
using Textinord.Trame.Domain.Entrepots;

namespace Textinord.Trame.Application.Tests.Outils;

public sealed class EntrepotGatewayEnMemoire : IEntrepotGateway
{
    private readonly ConcurrentQueue<(Entrepot Entrepot, OrdrePreparationEntrepot Ordre)> _recus = new();

    public IReadOnlyList<(Entrepot Entrepot, OrdrePreparationEntrepot Ordre)> Recus => _recus.ToArray();

    public Task TransmettreAsync(Entrepot entrepot, OrdrePreparationEntrepot ordre, CancellationToken cancellationToken)
    {
        _recus.Enqueue((entrepot, ordre));
        return Task.CompletedTask;
    }
}
