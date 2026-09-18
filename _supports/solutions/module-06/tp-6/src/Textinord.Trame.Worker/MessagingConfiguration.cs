using MassTransit;
using Textinord.Trame.Worker.Consumers;

namespace Textinord.Trame.Worker;

/// <summary>
/// Un seul point de configuration du bus. Le transport est choisi par configuration :
/// « InMemory » (défaut, aucune dépendance) ou « RabbitMq » (broker local ou Docker).
/// Le passage à Azure Service Bus suit le même schéma avec le package MassTransit.Azure.ServiceBus.Core.
/// </summary>
public static class MessagingConfiguration
{
    public const string TransportInMemory = "InMemory";
    public const string TransportRabbitMq = "RabbitMq";

    public static IServiceCollection AddTrameMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var transport = configuration["Messaging:Transport"] ?? TransportInMemory;

        services.AddMassTransit(bus =>
        {
            // Noms de files en kebab-case : « ordre-preparation » plutôt que « OrdrePreparationConsumer ».
            bus.SetKebabCaseEndpointNameFormatter();
            bus.AddConsumer<OrdrePreparationConsumer>();

            if (string.Equals(transport, TransportRabbitMq, StringComparison.OrdinalIgnoreCase))
            {
                bus.UsingRabbitMq((contexte, cfg) =>
                {
                    var section = configuration.GetSection("Messaging:RabbitMq");
                    cfg.Host(section["Host"] ?? "localhost", section["VirtualHost"] ?? "/", h =>
                    {
                        // Identifiants du broker de développement ; en production : Key Vault ou user-secrets.
                        h.Username(section["Username"] ?? "guest");
                        h.Password(section["Password"] ?? "guest");
                    });
                    ConfigurerEndpoints(contexte, cfg);
                });
            }
            else
            {
                bus.UsingInMemory((contexte, cfg) => ConfigurerEndpoints(contexte, cfg));
            }
        });

        return services;
    }

    private static void ConfigurerEndpoints<TEndpoint>(IBusRegistrationContext contexte, IBusFactoryConfigurator<TEndpoint> cfg)
        where TEndpoint : IReceiveEndpointConfigurator
    {
        // Nouvelle tentative à 1 s, 5 s puis 30 s ; au-delà, le message part en file d'erreur
        // (_error sur RabbitMQ, dead letter sur Service Bus) : c'est le « poison message ».
        cfg.UseMessageRetry(r => r.Intervals(
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)));
        cfg.ConfigureEndpoints(contexte);
    }
}
