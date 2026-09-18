# Cheatsheet — Module 5 : Communication, API et Cloud Azure

Condensé apprenant. Tout ce qui suit vise .NET 10 / C# 14 / ASP.NET Core 10.

## 1. Quel protocole pour quel besoin

| Protocole | Bon choix quand | Éviter pour | Trame 2 |
|---|---|---|---|
| REST + OpenAPI (JSON/HTTP) | clients variés, contrat lisible, API publique ou partenaires | flux temps réel, appels internes très fréquents | API commandes, catalogue, extranet, scanners, EDI |
| gRPC (Protobuf/HTTP2) | service à service, contrat strict, volume, latence, streaming | navigateurs (sauf gRPC-Web), partenaires non outillés | service stock interne (P95 < 300 ms) |
| SignalR (WebSocket) | pousser vers une interface | échanges fiables entre systèmes (pas de rejeu) | suivi de commande en direct |
| GraphQL | front qui agrège, besoins d'écran changeants | écritures transactionnelles, cache HTTP | écarté v1 (BFF suffisant) |
| Messaging (Service Bus, RabbitMQ) | découplage temporel, livraison garantie, pics | question-réponse immédiate | ordres de préparation (modules 2 et 6) |

Legacy : **WCF / SOAP** se lit et se migre. `dotnet-svcutil` pour appeler un SOAP tiers,
**CoreWCF** uniquement pour héberger un contrat existant pendant la migration.

## 2. Injection de dépendances

| Durée de vie | Une instance… | Pour | Jamais pour |
|---|---|---|---|
| `AddSingleton` | pour tout le processus | référentiels immuables, `IHttpClientFactory`, `IOptions`, caches thread-safe | tout ce qui garde un état par utilisateur ou une connexion |
| `AddScoped` | par requête HTTP / par `CreateScope()` | `DbContext`, service applicatif, unit of work | un `BackgroundService` sans scope explicite |
| `AddTransient` | à chaque injection | objets légers sans état, validateurs | objets coûteux à construire |

- **Règle** : un service ne dépend que de services de durée de vie égale ou plus longue. Un singleton qui reçoit un scoped = **dépendance captive**.
- **Filet** : `ValidateScopes` et `ValidateOnBuild` (activés en Development par `WebApplication.CreateBuilder`) ; pour un test : `new ServiceCollection().BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true })`.
- **Depuis un singleton** : `IServiceScopeFactory.CreateScope()` dans un `using`, résoudre le scoped dans le scope, jamais le stocker dans un champ.
- **Keyed services** : `AddKeyedSingleton<INotificateur, NotificateurSms>("sms")` puis `[FromKeyedServices("sms")] INotificateur n`.
- **Options** : `AddOptions<T>().Bind(config.GetSection("X")).ValidateDataAnnotations().ValidateOnStart()` ; injecter `IOptions<T>` (singleton), `IOptionsSnapshot<T>` (scoped, rechargé), `IOptionsMonitor<T>` (singleton, notifié).
- **HttpClient** : `AddHttpClient<ClientStock>(c => c.BaseAddress = ...)` + `.AddStandardResilienceHandler()` (package `Microsoft.Extensions.Http.Resilience`). Jamais `new HttpClient()` par appel.
- **Décorateur** : Scrutor `services.Decorate<IStore, StoreAvecCache>()`, ou fabrique `AddScoped<IStore>(sp => new StoreAvecCache(sp.GetRequiredService<StoreSql>()))`.

## 3. REST : verbes, codes, ProblemDetails

| Intention | Verbe | Succès | Échecs courants |
|---|---|---|---|
| Lister / filtrer / paginer | `GET /commandes?statut=Validee&page=2&taille=50` | 200 + page | 400 filtre invalide |
| Lire | `GET /commandes/{numero}` | 200 | 404 |
| Créer | `POST /commandes` | 201 + `Location` | 400 forme, 422 règle, 409 doublon |
| Remplacer / modifier | `PUT` / `PATCH /commandes/{numero}` | 200 ou 204 | 409 conflit (`ETag` / `If-Match`), 412 |
| Transition | `POST /commandes/{numero}/validation` | 200 | 409 transition interdite |
| Supprimer / annuler | `DELETE /commandes/{numero}` | 204 | 409 état incompatible |
| Non authentifié / non autorisé | — | — | 401 (`WWW-Authenticate: Bearer`) / 403 |
| Trop de requêtes | — | — | 429 + `Retry-After` |

**ProblemDetails (RFC 9457)** — une seule forme d'erreur, `Content-Type: application/problem+json` :

```json
{ "type": "https://trame.textinord.example/problemes/transition-interdite",
  "title": "Opération impossible dans l'état actuel de la commande",
  "status": 409, "detail": "Passage Annulee → Validee interdit.",
  "instance": "POST /api/v1/commandes/CMD-2026-004512/validation",
  "code": "TRANSITION_INTERDITE", "traceId": "00-…-01" }
```

`AddProblemDetails()` + `UseExceptionHandler()` + `UseStatusCodePages()` ;
`TypedResults.Problem(statusCode:, type:, title:, detail:, extensions:)` ;
`TypedResults.ValidationProblem(erreurs)` pour un 400 avec `errors` par champ.

- **Versioning** (`Asp.Versioning.Http`) : `UrlSegmentApiVersionReader` (`/api/v1/`), `ReportApiVersions` (en-tête `api-supported-versions`), `NewApiVersionSet().HasApiVersion(1.0).HasApiVersion(2.0)`, `MapToApiVersion(2.0)` sur un endpoint propre à une version. Ajouter un champ ne casse pas ; renommer ou retirer = nouvelle version + date de retrait (`Sunset`).
- **Pagination** : `page` + `taille` (plafond 100) ou curseur ; total et nombre de pages dans le corps.
- **Idempotence** : GET, PUT, DELETE par nature ; POST via `Idempotency-Key` (rejeu = même réponse, corps différent = 422).

## 4. Minimal API ASP.NET Core 10 : squelette

```csharp
var commandes = app.MapGroup("/api/v{version:apiVersion}/commandes")
    .WithApiVersionSet(versions).WithTags("Commandes")
    .RequireAuthorization("commandes.lecture")
    .RequireRateLimiting("api");

commandes.MapGet("/{numero}", Results<Ok<CommandeDetailV1>, ProblemHttpResult> (string numero, CommandesService s) =>
    s.Trouver(numero) is { } c ? TypedResults.Ok(c.VersDetailV1()) : Problemes.CommandeIntrouvable(numero))
    .MapToApiVersion(1.0).WithSummary("Détail d'une commande");

commandes.MapPost("/", Creer)
    .RequireAuthorization("commandes.ecriture")
    .AddEndpointFilter<ValidationFilter<CreerCommandeRequete>>();
```

| Brique | Une ligne | À retenir |
|---|---|---|
| Endpoint filter | `class F : IEndpointFilter { ValueTask<object?> InvokeAsync(ctx, next) }` + `.AddEndpointFilter<F>()` | construit une fois par endpoint : dépendances singleton |
| Validation intégrée .NET 10 | `builder.Services.AddValidation()` + DataAnnotations sur les paramètres | 400 automatique |
| Rate limiting | `AddRateLimiter(o => o.AddPolicy("api", ctx => RateLimitPartition.GetFixedWindowLimiter(cle, _ => new() { PermitLimit = 100, Window = 10 s })))` + `UseRateLimiter()` | `OnRejected` pour un ProblemDetails 429 |
| Output cache | `AddOutputCache(o => o.AddPolicy("ref", p => p.Expire(5 min)))`, `UseOutputCache()`, `.CacheOutput("ref")` | pas de cache par défaut sur les requêtes authentifiées |
| Health checks | `AddHealthChecks().AddCheck<X>("x", tags: ["ready"])`, `MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains("ready") })` | live ≠ ready |
| CORS | `AddCors(o => o.AddPolicy("extranet", p => p.WithOrigins(...).AllowAnyHeader().AllowAnyMethod()))`, `UseCors("extranet")` | jamais `AllowAnyOrigin` + credentials |
| OpenTelemetry | `AddOpenTelemetry().ConfigureResource(r => r.AddService("Textinord.Trame.Api")).WithTracing(t => t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddSource("…").AddOtlpExporter()).WithMetrics(m => m.AddAspNetCoreInstrumentation().AddRuntimeInstrumentation())` | `ActivitySource.StartActivity("commande.creer")` pour un span métier |

Ordre du pipeline : `UseExceptionHandler` → `UseStatusCodePages` → `UseCors` → `UseAuthentication` → `UseAuthorization` → `UseRateLimiter` → `UseOutputCache` → `Map…`.

## 5. OpenAPI : aide-mémoire

```csharp
builder.Services.AddApiVersioning(...).AddApiExplorer(o => { o.GroupNameFormat = "'v'VVV"; o.SubstituteApiVersionInUrl = true; })
    .AddOpenApi(o => { o.Document.AddDocumentTransformer<DocumentTrameTransformer>(); o.Document.AddOperationTransformer<ExigenceBearerTransformer>(); });
app.MapOpenApi().WithDocumentPerVersion();               // /openapi/v1.json, /openapi/v2.json
app.MapScalarApiReference(o => o.AddDocuments("v1", "v2")); // /scalar  (ou Swashbuckle : /swagger)
```

- Métadonnées : `.WithName`, `.WithSummary`, `.WithDescription`, `.WithTags`, `.Produces<T>(201)`, `.ProducesProblem(404)`, `.ProducesValidationProblem()` ; commentaires XML avec `<GenerateDocumentationFile>true`.
- Sécurité : `document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" }` ; sur l'opération, `operation.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] })`.
- Document au build : package `Microsoft.Extensions.ApiDescription.Server` → `obj/…/Textinord.Trame.Api.json` ; le versionner dans Git = test de contrat.
- Clients : `kiota generate -l CSharp -d openapi/v1.json -c TrameClient -n Textinord.Trame.Client -o ./Client` ; NSwag : `nswag openapi2csclient /input:openapi/v1.json /output:TrameClient.cs`.

## 6. P/Invoke et interop natif

```csharp
[LibraryImport("bascule", StringMarshalling = StringMarshalling.Utf8)]
[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
internal static partial int LirePoids(string port, out double kilogrammes);
```

| Sujet | À retenir |
|---|---|
| `LibraryImport` vs `DllImport` | générateur de source (.NET 7+) : marshalling visible à la compilation, compatible NativeAOT et trimming ; `DllImport` reste pour le code existant |
| Types blittables | `int`, `long`, `double`, `IntPtr`, `struct` de blittables : passés sans copie ; `bool` et `char` ne le sont pas (`[MarshalAs]`) |
| Chaînes | `StringMarshalling.Utf8` / `Utf16` ; côté natif, `const char*` |
| Tableaux, callbacks | `[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]`, `delegate* unmanaged<int, void>` ou `[UnmanagedFunctionPointer]` |
| Handles | toujours un `SafeHandle` dérivé : libération garantie, pas de fuite |
| Résolution de la bibliothèque | nom sans extension, `NativeLibrary.SetDllImportResolver`, `runtimes/<rid>/native/` dans le NuGet |
| COM (Windows) | `ComWrappers`, `[GeneratedComInterface]` / `[GeneratedComClass]` ; C++/CLI : Windows seulement, pas de NativeAOT |
| NativeAOT | pas de `DllImport` avec marshalling runtime, pas de COM classique, pas de chargement dynamique de code managé |

## 7. Identité : OIDC en cinq étapes, JWT bearer, Entra

1. Le client (extranet Blazor) redirige vers `/authorize` de l'IdP avec `client_id`, `redirect_uri`, `scope=openid profile api://trame/commandes.lecture`, `code_challenge` (PKCE).
2. L'utilisateur s'authentifie (mot de passe + MFA) ; l'IdP renvoie un **code** sur `/signin-oidc`.
3. Le client échange le code sur `/token` (code + `code_verifier` + secret si client confidentiel) → `id_token` (qui), `access_token` (pour l'API), `refresh_token`.
4. Le client appelle l'API avec `Authorization: Bearer <access_token>`.
5. L'API valide **localement** : signature (clés JWKS de l'IdP, en cache), `iss`, `aud`, `exp`, `nbf` ; puis claims → policies.

| Contexte | Configuration |
|---|---|
| Développement | `dotnet user-jwts create --name sofia --role adv` → section `Authentication:Schemes:Bearer` + clé dans les secrets utilisateur ; `AddAuthentication().AddJwtBearer()` sans paramètre lit la section |
| Production (Entra ID) | `AddMicrosoftIdentityWebApi(builder.Configuration)` (package `Microsoft.Identity.Web`), section `AzureAd` : `Instance`, `TenantId`, `ClientId`, `Audience` |
| Application web (cookies + OIDC) | `AddMicrosoftIdentityWebApp(builder.Configuration)` ; `AddOpenIdConnect()` pour un IdP tiers |
| Clients / partenaires | Entra **External ID** (ex B2C) ; salariés : Entra ID hybride avec l'AD (Entra Connect) ; fédération SAML 2.0 / OIDC par Entra |
| Rôles et scopes | app roles Entra → claim `roles` ; scopes délégués → claim `scp` ; `RequireRole`, `RequireClaim("scp", …)` |
| Legacy | WIF (WS-Fed, STS, `<system.identityModel>`) : remplacé par ASP.NET Core Authentication + OIDC ; le modèle de claims survit (`ClaimsPrincipal`) |

Piège : le mapping WS-* renomme `role` en `http://schemas.microsoft.com/ws/2008/06/identity/claims/role` ;
mettre `MapInboundClaims = false` et `RoleClaimType = "role"` (ou `"roles"` pour Entra) pour garder les noms courts.

## 8. Azure : services par usage

| Besoin | Service | Alternative |
|---|---|---|
| Héberger API / web / worker en containers | **Azure Container Apps** | App Service (sans containers), AKS (Kubernetes complet) |
| Traitement déclenché (fichier, file, timer) | **Azure Functions** | Container Apps Jobs |
| Base relationnelle | **Azure SQL Database** | Azure Database for PostgreSQL |
| Documents, catalogue | **Cosmos DB** | Azure AI Search pour le plein texte |
| Cache, sessions | **Azure Cache for Redis** / Managed Redis | — |
| Files et topics | **Service Bus** | Event Grid (événements), Event Hubs (flux) |
| Fichiers | **Blob Storage** | Azure Files (SMB) |
| Identité | **Entra ID**, **Entra External ID** | — |
| Secrets, configuration | **Key Vault**, **App Configuration** | variables d'environnement (dev) |
| Observabilité | **Azure Monitor + Application Insights** (OTLP / `Azure.Monitor.OpenTelemetry.AspNetCore`) | Grafana managé |
| Bord réseau, API publique | **Front Door** (WAF, TLS), **API Management** | Application Gateway |
| Images | **Container Registry** | — |

Modèles : **IaaS** (VM : vous gérez l'OS), **PaaS** (Container Apps, Azure SQL : vous gérez code et données),
**FaaS** (Functions : vous gérez des fonctions), **SaaS** (Entra, DevOps : vous gérez des utilisateurs).
Well-Architected Framework : fiabilité, sécurité, coûts, excellence opérationnelle, performance.
Architectures de référence : web n-tiers PaaS · micro-services sur Container Apps · serverless event-driven.

## 9. Containers, Aspire, déploiement : commandes

```bash
# Image sans Dockerfile (propriétés dans le csproj : ContainerRepository, ContainerImageTags, ContainerPort)
dotnet publish src/Textinord.Trame.Api -c Release /t:PublishContainer
dotnet publish ... /t:PublishContainer -p ContainerRegistry=acrtextinord.azurecr.io      # push direct
dotnet publish ... /t:PublishContainer -p ContainerArchiveOutputPath=./out/api.tar.gz   # sans démon Docker

# Dockerfile multi-stage
docker build -t textinord/trame-api:1.0.0 . && docker run --rm -p 8080:8080 textinord/trame-api:1.0.0
podman build -t textinord/trame-api:1.0.0 .   # même Dockerfile, sans démon, rootless

# Aspire 13 (SDK Aspire.AppHost.Sdk, package Aspire.Hosting.AppHost ; plus de workload)
dotnet run --project src/Textinord.Trame.AppHost   # dashboard : traces, logs, métriques
# AppHost : builder.AddProject<Projects.Textinord_Trame_Api>("trame-api").WithHttpHealthCheck("/health/ready").WithExternalHttpEndpoints()

# Azure Developer CLI (lit l'AppHost)
azd init && azd up      # ACR + environnement Container Apps + identités + déploiement
azd down                # supprime tout

# Azure CLI (à la main)
az group create -n rg-trame2-dev -l francecentral
az acr create -g rg-trame2-dev -n acrtextinordtrame2dev --sku Basic
az containerapp env create -g rg-trame2-dev -n cae-trame2-dev -l francecentral
az containerapp create -g rg-trame2-dev -n ca-trame-api --environment cae-trame2-dev \
  --image acrtextinordtrame2dev.azurecr.io/textinord/trame-api:1.0.0 \
  --target-port 8080 --ingress external --min-replicas 1 --max-replicas 3 \
  --registry-server acrtextinordtrame2dev.azurecr.io --registry-identity system --system-assigned
az group delete -n rg-trame2-dev --yes --no-wait
```

Rappels : TLS terminé par l'ingress (le container écoute en HTTP 8080) ; sondes `/health/live` et
`/health/ready` déclarées dans l'application container ; état **hors du processus** dès qu'il y a
deux réplicas ; secrets dans Key Vault, jamais dans l'image.
