using System.Text.Json.Serialization;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Textinord.Trame.Api.Commandes;
using Textinord.Trame.Api.Infrastructure;
using Textinord.Trame.Api.Referentiel;
using Textinord.Trame.Api.Securite;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------------------------
// 1. Services applicatifs et stockage en mémoire.
//    Les référentiels et le store sont des singletons (état partagé, immuable ou thread-safe) ;
//    le service applicatif est scoped (une instance par requête). Aucune dépendance captive :
//    un singleton ne reçoit jamais un scoped (voir exercice 5.2).
// ---------------------------------------------------------------------------------------------
builder.Services.AddSingleton<IReferentielArticles, ReferentielArticlesEnMemoire>();
builder.Services.AddSingleton<IReferentielClients, ReferentielClientsEnMemoire>();
builder.Services.AddSingleton<ICommandesStore, CommandesEnMemoire>();
builder.Services.AddSingleton<IGenerateurNumero, GenerateurNumeroEnMemoire>();
builder.Services.AddSingleton<IValidateur<CreerCommandeRequete>, CreerCommandeValidateur>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CommandesService>();

// Les enums (statuts) circulent en texte dans le JSON : « Validee », pas 2.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// ---------------------------------------------------------------------------------------------
// 2. Erreurs uniformes : ProblemDetails (RFC 9457) pour les exceptions, les codes 4xx sans corps
//    et les erreurs de validation. Le traceId permet de retrouver la requête dans les traces.
// ---------------------------------------------------------------------------------------------
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = contexte =>
    {
        contexte.ProblemDetails.Instance = $"{contexte.HttpContext.Request.Method} {contexte.HttpContext.Request.Path}";
        contexte.ProblemDetails.Extensions["traceId"] = System.Diagnostics.Activity.Current?.Id ?? contexte.HttpContext.TraceIdentifier;
    });

// ---------------------------------------------------------------------------------------------
// 3. Versioning par segment d'URL (/api/v1/..., /api/v2/...), exposition des versions
//    à l'API Explorer, et OpenAPI intégré (Microsoft.AspNetCore.OpenApi) : un document par
//    version, /openapi/v1.json et /openapi/v2.json. Les transformers ajoutent le schéma Bearer.
// ---------------------------------------------------------------------------------------------
builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    })
    .AddOpenApi(options =>
    {
        options.Document.AddDocumentTransformer<DocumentTrameTransformer>();
        options.Document.AddOperationTransformer<ExigenceBearerTransformer>();
    });

// ---------------------------------------------------------------------------------------------
// 5. Authentification JWT bearer et autorisation par politiques.
//    En local, la section Authentication:Schemes:Bearer est écrite par `dotnet user-jwts` ;
//    en production, on lira la section AzureAd avec Microsoft.Identity.Web (AddMicrosoftIdentityWebApi).
// ---------------------------------------------------------------------------------------------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // On garde les noms de claims du jeton (« sub », « role ») au lieu des URI WS-* héritées de WIF.
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "sub";
        options.TokenValidationParameters.RoleClaimType = "role";
    });

builder.Services.AddAuthorizationBuilder().AjouterPolitiquesTrame();

// ---------------------------------------------------------------------------------------------
// 6. Exploitation : limitation de débit, CORS pour l'extranet, cache de sortie, sondes de santé.
// ---------------------------------------------------------------------------------------------
builder.Services.AjouterRateLimitingTrame(builder.Configuration);
builder.Services.AjouterCacheTrame();

var originesExtranet = builder.Configuration.GetSection("Cors:Origines").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("extranet", politique => politique
    .WithOrigins(originesExtranet)
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddHealthChecks()
    .AddCheck<StockHealthCheck>("referentiel-articles", tags: ["ready"]);

// ---------------------------------------------------------------------------------------------
// 7. Observabilité : OpenTelemetry (traces, métriques, logs). Console en local si demandé,
//    OTLP dès qu'un endpoint est configuré (le dashboard Aspire injecte OTEL_EXPORTER_OTLP_ENDPOINT).
// ---------------------------------------------------------------------------------------------
var exportConsole = builder.Configuration.GetValue("Telemetrie:Console", false);
var exportOtlp = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(ressource => ressource.AddService(Telemetrie.NomService, serviceVersion: "1.0.0"))
    .WithTracing(traces =>
    {
        traces.AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"));
        traces.AddHttpClientInstrumentation();
        traces.AddSource(Telemetrie.Source.Name);
        if (exportConsole)
        {
            traces.AddConsoleExporter();
        }

        if (exportOtlp)
        {
            traces.AddOtlpExporter();
        }
    })
    .WithMetrics(metriques =>
    {
        metriques.AddAspNetCoreInstrumentation();
        metriques.AddHttpClientInstrumentation();
        metriques.AddRuntimeInstrumentation();
        metriques.AddMeter(Telemetrie.Meter.Name);
        if (exportOtlp)
        {
            metriques.AddOtlpExporter();
        }
    });

builder.Logging.AddOpenTelemetry(logs =>
{
    logs.IncludeFormattedMessage = true;
    logs.IncludeScopes = true;
    if (exportOtlp)
    {
        logs.AddOtlpExporter();
    }
});

var app = builder.Build();

// ---------------------------------------------------------------------------------------------
// Pipeline. Pas de redirection HTTPS ici : TLS est terminé par l'ingress de Container Apps
// (ou Front Door) ; le container n'écoute qu'en HTTP sur 8080.
// ---------------------------------------------------------------------------------------------
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseCors("extranet");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseOutputCache();

// Documentation : le document OpenAPI et Scalar sont publics en développement ; en production,
// on les réserve au réseau interne (API Management republie le contrat aux partenaires).
app.MapOpenApi().WithDocumentPerVersion();
app.MapScalarApiReference(options => options
    .WithTitle("Trame 2 — API commandes")
    .AddDocuments("v1", "v2"));

// Sondes : liveness (le processus répond) et readiness (les dépendances sont prêtes).
app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains("ready") });

var versions = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .HasApiVersion(new ApiVersion(2, 0))
    .ReportApiVersions()
    .Build();

app.MapCommandes(versions);
app.MapReferentiel(versions);

// Jeu de données de démonstration : trois commandes dans des états différents.
JeuDeDonnees.Amorcer(app.Services);

app.Run();

/// <summary>Point d'entrée rendu visible pour WebApplicationFactory (tests d'intégration).</summary>
public partial class Program;
