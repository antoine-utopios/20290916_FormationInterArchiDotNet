using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Textinord.Trame.Api.Tests;

/// <summary>
/// Héberge l'API en mémoire (TestServer) avec une configuration de test :
/// clé de signature connue pour fabriquer des jetons, limite de débit haute, télémétrie console coupée.
/// La configuration remplace ce que `dotnet user-jwts` aurait écrit dans les secrets utilisateur.
/// </summary>
public class TrameApiFactory : WebApplicationFactory<Program>
{
    public const string Issuer = "dotnet-user-jwts";

    public const string Audience = "http://localhost:5043";

    /// <summary>32 octets encodés en base64, comme le fait `dotnet user-jwts` dans les secrets utilisateur.</summary>
    public const string CleBase64 = "dGV4dGlub3JkLXRyYW1lMi1jbGUtZGUtdGVzdC0wMDE=";

    /// <summary>Limite de débit appliquée par cette fabrique ; surcharger pour tester le 429.</summary>
    protected virtual int PermitLimit => 10_000;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Schemes:Bearer:ValidIssuer"] = Issuer,
                ["Authentication:Schemes:Bearer:ValidAudiences:0"] = Audience,
                ["Authentication:Schemes:Bearer:SigningKeys:0:Issuer"] = Issuer,
                ["Authentication:Schemes:Bearer:SigningKeys:0:Value"] = CleBase64,
                ["Authentication:Schemes:Bearer:SigningKeys:0:Length"] = "32",
                ["RateLimiting:PermitLimit"] = PermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["RateLimiting:FenetreSecondes"] = "60",
                ["Telemetrie:Console"] = "false",
            });
        });

        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
    }
}

/// <summary>Fabrique dédiée au test de limitation de débit : trois requêtes par minute et par appelant.</summary>
public sealed class TrameApiFactoryLimitee : TrameApiFactory
{
    protected override int PermitLimit => 3;
}
