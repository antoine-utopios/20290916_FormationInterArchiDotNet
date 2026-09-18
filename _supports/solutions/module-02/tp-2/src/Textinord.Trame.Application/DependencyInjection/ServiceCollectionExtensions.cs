using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Textinord.Trame.Application.Commandes;
using Textinord.Trame.Application.Commandes.Messages;
using Textinord.Trame.Application.Commandes.Pipeline;
using Textinord.Trame.Application.Commandes.Traduction;
using Textinord.Trame.Application.Messaging;

namespace Textinord.Trame.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>Bus in-process, dead letter, historique, traducteur, pipeline et cas d'usage.</summary>
    public static IServiceCollection AddTrameApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<DeadLetterChannel>();
        services.TryAddSingleton<HistoriqueMessages>();
        services.TryAddSingleton<InProcessMessageBus>();
        services.TryAddSingleton<IMessageBus>(sp => sp.GetRequiredService<InProcessMessageBus>());
        services.TryAddSingleton<IMessageTranslator<CommandeParEntrepot, OrdrePreparationEntrepot>, OrdrePreparationTranslator>();
        services.TryAddSingleton<PipelineCommandes>();
        services.TryAddScoped<ValiderCommandeHandler>();

        return services;
    }
}
