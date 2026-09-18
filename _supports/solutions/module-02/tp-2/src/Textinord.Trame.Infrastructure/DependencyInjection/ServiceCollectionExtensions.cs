using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Textinord.Trame.Application.Commandes;
using Textinord.Trame.Domain.Commandes;
using Textinord.Trame.Infrastructure.Entrepots;
using Textinord.Trame.Infrastructure.Hosting;
using Textinord.Trame.Infrastructure.Persistance;
using Textinord.Trame.Infrastructure.Stock;

namespace Textinord.Trame.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>Adaptateurs de développement : dépôt en mémoire, stock en mémoire, passerelle journalisée, bus hébergé.</summary>
    public static IServiceCollection AddTrameInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<InMemoryCommandeRepository>();
        services.TryAddSingleton<ICommandeRepository>(sp => sp.GetRequiredService<InMemoryCommandeRepository>());
        services.TryAddSingleton<StockEnMemoire>();
        services.TryAddSingleton<IDisponibiliteStock>(sp => sp.GetRequiredService<StockEnMemoire>());
        services.TryAddSingleton<JournalEntrepotGateway>();
        services.TryAddSingleton<IEntrepotGateway>(sp => sp.GetRequiredService<JournalEntrepotGateway>());
        services.AddHostedService<MessageBusHostedService>();

        return services;
    }
}
