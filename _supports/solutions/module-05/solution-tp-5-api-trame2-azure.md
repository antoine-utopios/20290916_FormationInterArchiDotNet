# Solution — TP 5 : API Trame 2 prête pour Azure

> Document formateur — Ne pas distribuer avant la fin du TP.
> Code complet et compilable : `solutions/module-05/tp-5/` (`dotnet build`, `dotnet test`).

## Approche pédagogique

Le TP est long (90 minutes en autonomie complète) ; en séance, 60 minutes suffisent pour les
étapes 1 à 5 si les apprenants partent du code de départ fourni (domaine et référentiel) et
copient les blocs de configuration depuis la démo. Les étapes 6 (container, AppHost) et 7
(script Azure) se font en autonomie ou en démonstration depuis ce corrigé : elles n'exigent ni
Docker ni abonnement, mais elles demandent du temps de restauration NuGet (Aspire télécharge
son orchestrateur et son dashboard). Les points de contrôle 2, 4 et 5 sont ceux à vérifier
chez chacun : ProblemDetails uniformes, trace métier visible, tests indépendants.

## Structure du corrigé

```
tp-5/
  Textinord.Trame.sln
  Directory.Build.props                  # C# 14, nullable, analyzers, version 1.0.0
  .gitignore  .dockerignore  Dockerfile
  infra/deploy-aca.sh  infra/README.md
  src/Textinord.Trame.Api/
    Program.cs                           # composition : services, pipeline, endpoints
    Domaine/        Commande, LigneCommande, StatutCommande, CalculRemise (Remise), Result, Erreur
    Referentiel/    Article, Client, IReferentielArticles / Clients + implémentations en mémoire
    Commandes/      Contrats (DTO v1/v2, Page<T>), CommandesStore (ICommandesStore, IGenerateurNumero),
                    CommandesService (scoped), Validation (IValidateur<T>, ValidationFilter<T>),
                    CommandesEndpoints, ReferentielEndpoints
    Infrastructure/ Problemes (fabrique ProblemDetails), RateLimitingTrame (+ options), CacheTrame,
                    StockHealthCheck, Telemetrie (ActivitySource, Meter), OpenApiTransformers, JeuDeDonnees
    Securite/       Politiques (noms des policies et rôles)
  src/Textinord.Trame.AppHost/           # Aspire 13 : AppHost.cs, appsettings, launchSettings
  tests/Textinord.Trame.Api.Tests/
    TrameApiFactory.cs (+ TrameApiFactoryLimitee)   Jetons.cs
    CommandesEndpointsTests  SecuriteTests  VersioningEtOpenApiTests  ExploitationTests  Domaine/CalculRemiseTests
```

Le découpage suit une règle simple : `Domaine/` ne connaît rien d'HTTP ; `Commandes/` contient
les contrats, le port de persistance, le service applicatif et les endpoints ;
`Infrastructure/` regroupe ce qui est technique et réutilisable par d'autres ressources.

## Les choix, un par un

### Durées de vie

| Service | Durée de vie | Pourquoi |
|---|---|---|
| `IReferentielArticles`, `IReferentielClients` | singleton | données immuables (`FrozenDictionary`), partagées |
| `ICommandesStore` (`CommandesEnMemoire`) | singleton | l'état de l'application, `ConcurrentDictionary` ; sera un `DbContext` scoped au module 4 |
| `IGenerateurNumero` | singleton | compteur par année, `ConcurrentDictionary.AddOrUpdate` atomique |
| `IValidateur<CreerCommandeRequete>` | singleton | sans état ; obligatoire car l'endpoint filter est construit une fois par endpoint avec les services de l'application |
| `TimeProvider.System` | singleton | l'horloge injectée rend la date de commande testable |
| `CommandesService` | scoped | une instance par requête, orchestre domaine + référentiels + télémétrie |

Aucune dépendance captive : les scoped ne sont consommés que par les handlers.

### Versioning (`Asp.Versioning.Http` + `Asp.Versioning.OpenApi`)

- Lecteur `UrlSegmentApiVersionReader`, groupe `/api/v{version:apiVersion}/commandes`, jeu
  de versions 1.0 et 2.0 avec `ReportApiVersions` (en-tête `api-supported-versions: 1.0, 2.0`
  vérifié par un test).
- Les endpoints communs (liste, création, validation, annulation) ne sont pas mappés à une
  version : ils servent implicitement toutes les versions du jeu. Les deux `GET /{numero}` sont
  mappés explicitement (`MapToApiVersion(1.0)` / `(2.0)`).
- Le `Location` du POST respecte la version demandée grâce à `http.RequestedApiVersion`,
  propriété d'extension C# 14 fournie par Asp.Versioning 10 (`using Microsoft.AspNetCore.Http`
  implicite dans le SDK web).
- Une version inexistante dans l'URL (`/api/v3/…`) ne correspond à aucun endpoint : 404,
  transformé en ProblemDetails par `UseStatusCodePages`. Avec un lecteur par en-tête ou query
  string, Asp.Versioning répondrait 400 « Unsupported API version » ; le test documente ce choix.

### ProblemDetails

- `AddProblemDetails` avec `CustomizeProblemDetails` : `instance` = méthode + chemin,
  extension `traceId` = `Activity.Current?.Id` (corrélable avec OpenTelemetry).
- `UseExceptionHandler()` transforme toute exception non gérée en 500 ProblemDetails ;
  `UseStatusCodePages()` habille les 401 / 403 / 404 sans corps.
- La fabrique `Problemes` centralise les erreurs applicatives : 404 commande introuvable, 422
  client ou article inconnu, 409 conflit métier avec `code` = code du `Result` du domaine. Les
  `type` sont des URI stables sous `https://trame.textinord.example/problemes/`.
- La validation de forme passe par `ValidationFilter<T>` (endpoint filter générique) et
  `TypedResults.ValidationProblem` : 400 avec `errors` par champ (`lignes[0].quantite`).
- Le 429 du rate limiter écrit lui aussi un ProblemDetails via `IProblemDetailsService`
  (`OnRejected`), avec `Retry-After`.

### OpenAPI et Scalar

- `AddApiExplorer` (format `'v'VVV`, substitution de la version dans l'URL) puis `.AddOpenApi(...)`
  sur le builder de versioning : un document par version, nommés `v1` et `v2` ;
  `app.MapOpenApi().WithDocumentPerVersion()` publie `/openapi/v1.json` et `/openapi/v2.json`.
- `DocumentTrameTransformer` renseigne `Info` (titre, version, contact Sofia Marques) et
  déclare le schéma de sécurité `Bearer` ; `ExigenceBearerTransformer` ajoute l'exigence sur
  chaque opération portant une métadonnée `IAuthorizeData` sans `IAllowAnonymous`.
- Scalar : `MapScalarApiReference(o => o.WithTitle(...).AddDocuments("v1", "v2"))` sur `/scalar`.
- `Microsoft.OpenApi` est référencé explicitement en 2.12.2 : la version transitive 2.0.0
  déclenche l'avertissement NU1903 (vulnérabilité connue).
- `GenerateDocumentationFile` est activé (avec `NoWarn CS1591`) : les commentaires XML des
  records apparaissent dans les schémas.

### JWT bearer et politiques

- `AddJwtBearer` sans section explicite : la configuration `Authentication:Schemes:Bearer`
  (émetteur, audiences, clés de signature) est lue automatiquement — c'est le contrat de
  `dotnet user-jwts`. `appsettings.Development.json` contient l'émetteur et les audiences ;
  la clé va dans les secrets utilisateur (`UserSecretsId` dans le csproj).
- `MapInboundClaims = false`, `NameClaimType = "sub"`, `RoleClaimType = "role"` : on garde les
  noms courts du jeton au lieu des URI WS-* héritées de WIF ; `RequireRole("adv")` fonctionne
  alors avec `dotnet user-jwts create --role adv`.
- Deux politiques dans `Politiques` : `commandes.lecture` (`adv`, `entrepot`, `partenaire`)
  sur le groupe, `commandes.ecriture` (`adv`) sur POST, validation, DELETE.
- Pas de redirection HTTPS : TLS est terminé par l'ingress Container Apps ; le container
  n'écoute qu'en HTTP 8080.

### Exploitation

- **Health checks** : `/health/live` (aucun check, prédicat `_ => false`) et `/health/ready`
  (checks tagués `ready`, ici `StockHealthCheck`).
- **Rate limiting** : `RateLimitingOptions` liées à la section `RateLimiting`, validées par
  DataAnnotations au démarrage ; `RateLimiterOptions` configurées via `IOptions<RateLimitingOptions>`
  (lecture paresseuse : indispensable pour que la configuration injectée par
  `WebApplicationFactory` soit prise en compte, car elle n'est appliquée qu'au `Build()`).
  Partition par nom d'utilisateur authentifié, sinon adresse IP. Ordre du pipeline :
  `UseAuthentication` → `UseAuthorization` → `UseRateLimiter` pour que l'identité soit connue.
- **Output cache** : politique `referentiel` (5 minutes, tag) sur `GET /api/v1/referentiel/statuts`,
  endpoint anonyme (le cache de sortie ignore par défaut les requêtes authentifiées).
- **CORS** : politique `extranet`, origines en configuration (`Cors:Origines`).
- **OpenTelemetry** : ressource `Textinord.Trame.Api`, traces (ASP.NET Core sans `/health`,
  HttpClient, `ActivitySource` métier), métriques (ASP.NET Core, HttpClient, runtime, compteurs
  `trame.commandes.*`), logs enrichis ; console si `Telemetrie:Console`, OTLP si
  `OTEL_EXPORTER_OTLP_ENDPOINT` est défini (ce que fait l'AppHost Aspire).

### Tests (`WebApplicationFactory`)

- `TrameApiFactory` injecte en mémoire : émetteur, audience, clé de signature (32 octets en
  base64, comme `user-jwts`), limite de débit 10 000, `Telemetrie:Console = false`.
  `TrameApiFactoryLimitee` (limite 3) sert au test du 429.
- `Jetons` fabrique des JWT HS256 avec la même clé (`JwtSecurityTokenHandler`, claims `sub` et
  `role`) et fournit des clients HTTP `Adv()`, `Entrepot()`, `Anonyme()`.
- 28 tests : endpoints (9), sécurité (4), versioning et OpenAPI (5 dont un `Theory` à deux cas),
  exploitation (4 dont un `Theory` à deux cas), domaine (6 dont un `Theory` à cinq cas). Chaque
  test crée ses propres commandes : aucune dépendance à l'ordre.

Résultat attendu :

```
Réussi!  - échec :     0, réussite :    28, ignorée(s) :     0, total :    28
```

### Container : Dockerfile et PublishContainer

- Le csproj porte `ContainerRepository` = `textinord/trame-api`, tags `1.0.0` et `latest`,
  image de base `aspnet:10.0`, utilisateur `app`, port 8080, variables `ASPNETCORE_HTTP_PORTS`,
  labels OCI. `dotnet publish -c Release /t:PublishContainer` produit l'image ; sans démon,
  `-p ContainerArchiveOutputPath=./out/trame-api.tar.gz` produit une archive chargeable par
  `docker load` ou `podman load`.
- Le `Dockerfile` multi-stage (SDK → runtime, restauration en couche séparée, `USER $APP_UID`)
  donne la même image pour les pipelines qui l'exigent. `.dockerignore` exclut `bin`, `obj`,
  tests et infra.

### AppHost .NET Aspire 13

- Projet `Textinord.Trame.AppHost` avec `Sdk="Aspire.AppHost.Sdk/13.5.3"` et le package
  `Aspire.Hosting.AppHost` : aucun workload. `AddProject<Projects.Textinord_Trame_Api>("trame-api")`
  avec `WithHttpHealthCheck("/health/ready")` et `WithExternalHttpEndpoints()`.
- `Trame:Replicas` (configuration) permet de lancer plusieurs instances pour montrer que le
  stockage en mémoire n'est pas partagé : chaque réplica a ses commandes. C'est l'argument du
  module 4.
- `AspireUseCliBundle` est à `false` (avertissement ASPIRE010 supprimé) : `dotnet run` suffit ;
  le CLI `aspire` s'installe séparément si besoin.
- L'AppHost est référencé par la solution et compile sur macOS sans Docker ; l'exécuter demande
  que l'orchestrateur DCP (téléchargé par NuGet) puisse démarrer, ce qui ne nécessite pas Docker
  tant qu'aucune ressource container n'est déclarée.

### Script `infra/deploy-aca.sh`

Huit étapes commentées : `az login`, groupe de ressources (France Centre, tags), ACR Basic,
`dotnet publish /t:PublishContainer -p ContainerRegistry=…` (image poussée sans Docker),
Log Analytics + environnement Container Apps, `az containerapp create` (ingress externe 8080,
1 à 3 réplicas, identité système, variables d'environnement), rôle AcrPull, sondes
`/health/live` et `/health/ready` injectées dans le YAML, vérification `curl`, option
`--teardown`. Le commentaire final situe `azd init` / `azd up` comme équivalent piloté par
l'AppHost. `infra/README.md` documente le plan B local complet.

## Variantes acceptables

1. Validation par `builder.Services.AddValidation()` et DataAnnotations (.NET 10) à la place du
   filtre générique : correcte, plus courte ; vérifier que les messages d'erreur restent en
   français et que le 400 est bien un `ValidationProblemDetails`.
2. Un seul document OpenAPI pour les deux versions : acceptable mais moins lisible pour les
   partenaires ; faire remarquer le paramètre `version` qui apparaît dans les chemins si
   `SubstituteApiVersionInUrl` n'est pas activé.
3. Handler d'authentification de test (`TestAuthHandler`) à la place de vrais JWT dans les
   tests : acceptable, mais les tests 401 (jeton mal signé) et le mapping des rôles ne sont
   alors plus couverts.
4. Contrôleurs MVC à la place des Minimal APIs : acceptable si `ProblemDetails`, versioning
   (`[ApiVersion]`) et OpenAPI sont équivalents ; les points de la grille restent les mêmes.
5. `Swashbuckle` (Swagger UI) à la place de Scalar : équivalent.

## Erreurs classiques à repérer en correction

| Erreur observée | Cause probable | Comment corriger |
|---|---|---|
| 401 en test alors que le jeton est bon | clé, émetteur ou audience de test différents de ceux lus par `AddJwtBearer` | même section `Authentication:Schemes:Bearer` dans la fabrique de test et même clé dans `Jetons` |
| 403 pour `adv` | claim `role` renommé par le mapping WS-* | `MapInboundClaims = false`, `RoleClaimType = "role"` |
| La limite de débit de test est ignorée | options lues avec `builder.Configuration` au démarrage | Options pattern, résolution paresseuse via `IOptions<T>` |
| `GET /api/v1/…/{numero}` renvoie la v2 | un seul des deux endpoints est mappé à sa version | `MapToApiVersion` sur les deux |
| Tests dépendants de l'ordre | commandes partagées entre tests, numéros supposés | chaque test crée ce dont il a besoin et lit le numéro dans la réponse |
| `ValidationFilter<T>` reçoit un validateur scoped | filtre construit une fois avec les services de l'application | validateur singleton, ou résoudre le validateur depuis `context.HttpContext.RequestServices` |
| `/health` apparaît dans les traces | instrumentation ASP.NET Core sans filtre | `AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"))` |
| Avertissements AV0029 / AV0030 | `AddOpenApi("v1")` manuel avec Asp.Versioning 10 | `AddApiVersioning().AddApiExplorer().AddOpenApi()` et `MapOpenApi().WithDocumentPerVersion()` |
| NU1903 sur `Microsoft.OpenApi` | dépendance transitive 2.0.0 vulnérable | référence explicite à 2.12.2 ou plus récent |
| `PublishContainer` échoue sans Docker | pas de démon | `ContainerArchiveOutputPath` |

## Points à insister en débriefing

- Le handler traduit un `Result` en code HTTP : toute règle métier dans un handler est une
  régression par rapport au TP 1.
- Une seule forme d'erreur, des `type` stables : c'est ce qui rend l'API utilisable par un
  client généré sans lire la documentation.
- Le stockage en mémoire est un mensonge assumé : deux réplicas, deux jeux de commandes. Le
  module 4 corrige ; Container Apps avec `--min-replicas 1 --max-replicas 3` rend la question
  concrète.
- Tout ce qui est configuré ici (sondes, télémétrie, image, identité) est ce que le script
  `deploy-aca.sh` et Container Apps consomment : l'API a été conçue pour être exploitée, pas
  seulement pour répondre.

## Bonus

Les apprenants rapides peuvent : ajouter un en-tête `Idempotency-Key` sur le POST (dictionnaire
clé → réponse, 24 h), publier le document OpenAPI au build avec
`Microsoft.Extensions.ApiDescription.Server` et le versionner dans Git avec un test de
non-régression, ou remplacer l'export console par `Azure.Monitor.OpenTelemetry.AspNetCore`
(chaîne de connexion Application Insights en configuration) pour préparer le module 6.
