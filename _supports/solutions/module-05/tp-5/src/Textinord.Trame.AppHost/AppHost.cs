// Orchestrateur .NET Aspire de Trame 2 — développement local.
// `dotnet run --project src/Textinord.Trame.AppHost` démarre l'API et ouvre le dashboard
// (traces, logs structurés, métriques) alimenté en OTLP par l'API elle-même.
//
// Aux modules suivants, on ajoutera ici les dépendances :
//   var redis = builder.AddRedis("cache");                        // module 4
//   var bus   = builder.AddRabbitMQ("bus");                       // module 6 (RabbitMQ local, Service Bus en production)
//   api.WithReference(redis).WithReference(bus).WaitFor(bus);
// et `azd init` puis `azd up` déploieront le tout sur Azure Container Apps.

using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.Textinord_Trame_Api>("trame-api")
    .WithHttpHealthCheck("/health/ready")
    .WithExternalHttpEndpoints();

// Réplicas : Aspire peut lancer plusieurs instances pour vérifier qu'on est bien stateless.
// Avec le stockage en mémoire de ce TP, deux réplicas donneraient deux jeux de commandes :
// c'est la démonstration (volontaire) qu'il faut sortir l'état du processus (module 4).
if (builder.Configuration.GetValue("Trame:Replicas", 1) > 1)
{
    api.WithReplicas(builder.Configuration.GetValue("Trame:Replicas", 1));
}

builder.Build().Run();
