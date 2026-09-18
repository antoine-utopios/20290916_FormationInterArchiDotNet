using Microsoft.Extensions.Hosting;
using Textinord.Trame.Application.Commandes.Pipeline;
using Textinord.Trame.Application.Messaging;

namespace Textinord.Trame.Infrastructure.Hosting;

/// <summary>
/// Démarre le bus avec l'hôte et l'arrête proprement (canaux vidés) à l'arrêt de l'hôte.
/// Le pipeline est résolu avant le démarrage : c'est lui qui déclare les canaux et les abonnements.
/// </summary>
public sealed class MessageBusHostedService(IMessageBus bus, PipelineCommandes pipeline) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return bus.DemarrerAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => bus.ArreterAsync(cancellationToken);
}
