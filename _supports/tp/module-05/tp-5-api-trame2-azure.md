# TP 5 — API Trame 2 prête pour Azure

> Module : 5 — Communication, API et Cloud Azure
> Durée estimée : 90 min (60 min en séance sur les étapes 1 à 5 ; étapes 6 et 7 en autonomie ou en démonstration)
> Difficulté : 4 / 5
> Type : TP individuel ou en binôme, projet .NET 10 complet

## Mise en situation

Julien Delcourt a validé l'ADR « API commandes » à partir des contrats de l'exercice 5.1.
Sofia Marques vous confie l'implémentation de la première tranche : une Minimal API
ASP.NET Core 10, versionnée, sécurisée par jeton, documentée, observable, testée, et
livrable sous forme d'image de container sur Azure Container Apps. La persistance reste en
mémoire : le module 4 a préparé EF Core, le branchement se fera au TP 6.

Ce que Nadia Benali attend pour donner son feu vert au déploiement : « une API qu'on peut
appeler avec un jeton Entra ID, qui répond proprement quand ça se passe mal, qu'on peut
surveiller, et qu'on sait déployer sans copier des fichiers à la main ».

## Prérequis

- SDK .NET 10.0.3xx (`dotnet --version`), `DOTNET_CLI_TELEMETRY_OPTOUT=1`
- Accès NuGet (les packages : `Asp.Versioning.Http`, `Asp.Versioning.OpenApi`, `Scalar.AspNetCore`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `OpenTelemetry.*`, `Microsoft.AspNetCore.Mvc.Testing`, `Aspire.Hosting.AppHost`)
- Docker et Azure sont **facultatifs** : chaque étape a un plan B local
- Avoir fait la démo 5.1 et l'exercice 5.1 (votre tableau de contrat sert de spécification)

## Point de départ

Le TP est autonome : vous partez d'un dépôt vide et des fichiers ci-dessous. Créez la
structure suivante, puis copiez le code de départ fourni (domaine et référentiel en mémoire).

```bash
mkdir trame2-api && cd trame2-api
dotnet new sln -n Textinord.Trame --format sln
dotnet new webapi --use-minimal-apis -n Textinord.Trame.Api -o src/Textinord.Trame.Api
dotnet new xunit -n Textinord.Trame.Api.Tests -o tests/Textinord.Trame.Api.Tests
dotnet sln add src/Textinord.Trame.Api tests/Textinord.Trame.Api.Tests
dotnet add tests/Textinord.Trame.Api.Tests reference src/Textinord.Trame.Api
dotnet add tests/Textinord.Trame.Api.Tests package Microsoft.AspNetCore.Mvc.Testing
```

### Code de départ : le domaine (dossier `Domaine/`)

Recopiez tel quel dans `src/Textinord.Trame.Api/Domaine/` — c'est le noyau métier du TP 1,
allégé (statuts, remise plafonnée à 30 %, machine à états, pattern Result).

```csharp
// Domaine/StatutCommande.cs
namespace Textinord.Trame.Api.Domaine;

public enum StatutCommande { Brouillon, EnAttenteStock, Validee, EnPreparation, Expediee, Facturee, Annulee }
```

```csharp
// Domaine/Result.cs
namespace Textinord.Trame.Api.Domaine;

public sealed record Erreur(string Code, string Message);

public sealed class Result
{
    private Result(bool estSucces, Erreur? erreur) { EstSucces = estSucces; Erreur = erreur; }
    public bool EstSucces { get; }
    public bool EstEchec => !EstSucces;
    public Erreur? Erreur { get; }
    public static Result Ok() => new(true, null);
    public static Result Echec(string code, string message) => new(false, new Erreur(code, message));
}
```

```csharp
// Domaine/CalculRemise.cs
namespace Textinord.Trame.Api.Domaine;

public sealed record Remise(decimal TauxClient, decimal TauxVolume, decimal TauxApplique)
{
    public bool PlafondAtteint => TauxClient + TauxVolume >= CalculRemise.Plafond;
}

public static class CalculRemise
{
    public const int SeuilVolume = 500;
    public const decimal TauxVolume = 0.05m;
    public const decimal Plafond = 0.30m;

    public static Remise Calculer(decimal tauxClient, int quantite)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tauxClient);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantite);
        var tauxVolume = quantite > SeuilVolume ? TauxVolume : 0m;
        return new Remise(tauxClient, tauxVolume, Math.Min(tauxClient + tauxVolume, Plafond));
    }
}
```

```csharp
// Domaine/LigneCommande.cs
namespace Textinord.Trame.Api.Domaine;

public sealed class LigneCommande
{
    internal LigneCommande(int numero, string reference, int quantite, decimal prixUnitaire, Remise remise)
    {
        Numero = numero; Reference = reference; Quantite = quantite; PrixUnitaire = prixUnitaire; Remise = remise;
    }

    public int Numero { get; }
    public string Reference { get; }
    public int Quantite { get; }
    public decimal PrixUnitaire { get; }
    public Remise Remise { get; }
    public string? EntrepotAffecte { get; internal set; }
    public decimal MontantBrut => PrixUnitaire * Quantite;
    public decimal MontantRemise => Math.Round(MontantBrut * Remise.TauxApplique, 2, MidpointRounding.ToEven);
    public decimal MontantNet => MontantBrut - MontantRemise;
}
```

```csharp
// Domaine/Commande.cs
using System.Collections.Frozen;

namespace Textinord.Trame.Api.Domaine;

public sealed class Commande
{
    private static readonly FrozenDictionary<StatutCommande, StatutCommande[]> Transitions =
        new Dictionary<StatutCommande, StatutCommande[]>
        {
            [StatutCommande.Brouillon] = [StatutCommande.Validee, StatutCommande.EnAttenteStock, StatutCommande.Annulee],
            [StatutCommande.EnAttenteStock] = [StatutCommande.Validee, StatutCommande.Annulee],
            [StatutCommande.Validee] = [StatutCommande.EnPreparation, StatutCommande.Annulee],
            [StatutCommande.EnPreparation] = [StatutCommande.Expediee],
            [StatutCommande.Expediee] = [StatutCommande.Facturee],
            [StatutCommande.Facturee] = [],
            [StatutCommande.Annulee] = [],
        }.ToFrozenDictionary();

    private readonly List<LigneCommande> _lignes = [];
    private readonly Lock _verrou = new();

    public Commande(string numero, string codeClient, decimal tauxRemiseClient, DateOnly date)
    {
        Numero = numero; CodeClient = codeClient; TauxRemiseClient = tauxRemiseClient; Date = date;
    }

    public string Numero { get; }
    public string CodeClient { get; }
    public decimal TauxRemiseClient { get; }
    public DateOnly Date { get; }
    public StatutCommande Statut { get; private set; } = StatutCommande.Brouillon;
    public IReadOnlyList<LigneCommande> Lignes => _lignes;
    public int NombrePieces => _lignes.Sum(l => l.Quantite);
    public decimal TotalBrut => _lignes.Sum(l => l.MontantBrut);
    public decimal TotalRemises => _lignes.Sum(l => l.MontantRemise);
    public decimal TotalHT => _lignes.Sum(l => l.MontantNet);
    public bool EstModifiable => Statut is StatutCommande.Brouillon or StatutCommande.EnAttenteStock;

    public LigneCommande AjouterLigne(string reference, int quantite, decimal prixUnitaire)
    {
        lock (_verrou)
        {
            if (!EstModifiable) throw new InvalidOperationException($"La commande {Numero} est {Statut}.");
            var ligne = new LigneCommande(_lignes.Count + 1, reference, quantite, prixUnitaire, CalculRemise.Calculer(TauxRemiseClient, quantite));
            _lignes.Add(ligne);
            return ligne;
        }
    }

    public Result Valider(Func<string, int, string?> entrepotPouvantServir)
    {
        lock (_verrou)
        {
            if (_lignes.Count == 0) return Result.Echec("COMMANDE_VIDE", "Une commande sans ligne ne se valide pas.");
            if (!PeutPasserA(StatutCommande.Validee)) return TransitionInterdite(StatutCommande.Validee);

            var affectations = _lignes.Select(l => (Ligne: l, Entrepot: entrepotPouvantServir(l.Reference, l.Quantite))).ToList();
            if (affectations.Any(a => a.Entrepot is null)) { Statut = StatutCommande.EnAttenteStock; return Result.Ok(); }

            foreach (var (ligne, entrepot) in affectations) ligne.EntrepotAffecte = entrepot;
            Statut = StatutCommande.Validee;
            return Result.Ok();
        }
    }

    public Result Annuler()
    {
        lock (_verrou)
        {
            if (!PeutPasserA(StatutCommande.Annulee)) return TransitionInterdite(StatutCommande.Annulee);
            Statut = StatutCommande.Annulee;
            return Result.Ok();
        }
    }

    public bool PeutPasserA(StatutCommande cible) => Transitions[Statut].Contains(cible);
    public static IReadOnlyList<StatutCommande> TransitionsDepuis(StatutCommande statut) => Transitions[statut];

    private Result TransitionInterdite(StatutCommande cible) =>
        Result.Echec("TRANSITION_INTERDITE", $"Passage {Statut} → {cible} interdit pour la commande {Numero}.");
}
```

### Code de départ : le référentiel en mémoire (dossier `Referentiel/`)

```csharp
// Referentiel/Referentiel.cs
using System.Collections.Frozen;

namespace Textinord.Trame.Api.Referentiel;

public sealed record Article(string Reference, string Libelle, string Famille, decimal PrixBase, IReadOnlyDictionary<string, int> StockParEntrepot)
{
    public string? EntrepotPouvantServir(int quantite) =>
        StockParEntrepot.Where(kv => kv.Value >= quantite).OrderBy(kv => kv.Key == "RBX" ? 0 : 1).Select(kv => kv.Key).FirstOrDefault();
}

public sealed record Client(string Code, string RaisonSociale, string ConditionTarifaire, decimal TauxRemise);

public interface IReferentielArticles { Article? Trouver(string reference); IReadOnlyCollection<Article> Tous(); }

public interface IReferentielClients { Client? Trouver(string code); }

public sealed class ReferentielArticlesEnMemoire : IReferentielArticles
{
    private static readonly FrozenDictionary<string, Article> Articles = new[]
    {
        Creer("VT-PARKA-XL", "Parka haute visibilité classe 3, taille XL", "Vêtements de travail", 64.90m, 1_200, 300),
        Creer("VT-PANT-44", "Pantalon multipoches, taille 44", "Vêtements de travail", 29.50m, 2_400, 800),
        Creer("EPI-GANT-09", "Gants anti-coupure niveau D, taille 9", "EPI textiles", 7.80m, 0, 0),
        Creer("EPI-GILET-L", "Gilet de signalisation, taille L", "EPI textiles", 4.20m, 5_000, 5_000),
        Creer("HOT-DRAP-160", "Drap plat percale 160 x 290", "Linge hôtelier", 18.40m, 0, 950),
        Creer("HOT-SERV-50", "Serviette éponge 50 x 100, 500 g", "Linge hôtelier", 5.60m, 3_000, 1_200),
    }.ToFrozenDictionary(a => a.Reference, StringComparer.OrdinalIgnoreCase);

    public Article? Trouver(string reference) => Articles.TryGetValue(reference, out var a) ? a : null;
    public IReadOnlyCollection<Article> Tous() => Articles.Values;

    private static Article Creer(string reference, string libelle, string famille, decimal prix, int rbx, int lsq) =>
        new(reference, libelle, famille, prix, new Dictionary<string, int> { ["RBX"] = rbx, ["LSQ"] = lsq });
}

public sealed class ReferentielClientsEnMemoire : IReferentielClients
{
    private static readonly FrozenDictionary<string, Client> Clients = new Client[]
    {
        new("CLI-0042", "Ville de Roubaix — services techniques", "COL", 0.10m),
        new("CLI-0107", "Hôtel Grand Nord, Lille", "HOT", 0.12m),
        new("CLI-0311", "Aciéries de Denain", "IND", 0.15m),
        new("CLI-0500", "Groupe hospitalier de la Métropole", "GC", 0.25m),
    }.ToFrozenDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

    public Client? Trouver(string code) => Clients.TryGetValue(code, out var c) ? c : null;
}
```

## Étapes

### Étape 1 — Contrats, stockage et service applicatif (10 min)

1. Créez les records de contrat dans `Commandes/Contrats.cs` : `CreerCommandeRequete(CodeClient, Lignes)`, `LigneRequete(Reference, Quantite, PrixNegocie?)`, `CommandeResume`, `CommandeDetailV1` (une ligne = référence, quantité, prix, **taux de remise unique**, montant net, entrepôt), `Page<T>(Elements, Numero, Taille, Total)`.
2. Créez `ICommandesStore` (Trouver, Ajouter, Rechercher paginé avec filtre statut et client) et son implémentation `CommandesEnMemoire` sur un `ConcurrentDictionary`, plus `IGenerateurNumero` (`CMD-AAAA-NNNNNN`, séquentiel par année, démarrez à 4 512).
3. Créez `CommandesService` (scoped) qui orchestre : recherche paginée, création (client et articles vérifiés dans les référentiels, prix négocié ou prix de base, remise calculée par le domaine), validation (stock via `Article.EntrepotPouvantServir`), annulation.
4. Enregistrez les services dans `Program.cs` avec les **bonnes durées de vie**, et `ConfigureHttpJsonOptions` pour sérialiser les enums en texte.

Point de contrôle 1 : `dotnet build` sans avertissement ; vous savez expliquer pourquoi le store est singleton et le service scoped.

### Étape 2 — Endpoints versionnés, ProblemDetails, validation (15 min)

1. Ajoutez `Asp.Versioning.Http` et `Asp.Versioning.OpenApi`. Configurez le versioning par segment d'URL (`/api/v{version:apiVersion}/commandes`), version par défaut 1.0, `ReportApiVersions`.
2. Implémentez dans un groupe versionné : `GET /` (page, taille plafonnée à 100, filtres `statut` et `client`), `GET /{numero}` (v1), `POST /` (201 + `Location`), `POST /{numero}/validation`, `DELETE /{numero}` (annulation, 204).
3. `AddProblemDetails()` + `UseExceptionHandler()` + `UseStatusCodePages()`. Créez une fabrique `Problemes` : 404 « commande introuvable », 422 « client inconnu » / « article inconnu », 409 « conflit métier » (avec l'extension `code` reprenant le code du `Result`). Chaque `type` est une URI stable sous `https://trame.textinord.example/problemes/`.
4. Écrivez un **endpoint filter** générique `ValidationFilter<T>` qui appelle un `IValidateur<T>` et répond `TypedResults.ValidationProblem` (400) ; appliquez-le au POST avec un validateur de forme (code client obligatoire, au moins une ligne, quantité > 0, prix négocié > 0, au plus 200 lignes).

Point de contrôle 2 : avec `curl`, un POST invalide donne 400 `application/problem+json` avec la liste des champs ; un GET sur un numéro inconnu donne 404 avec un `type` et un `traceId`.

### Étape 3 — OpenAPI, Scalar et version 2 (10 min)

1. `AddApiExplorer` (format de groupe `'v'VVV`, substitution de la version dans l'URL) puis `.AddOpenApi(...)` sur le builder de versioning ; `app.MapOpenApi().WithDocumentPerVersion()` ; `MapScalarApiReference` avec les documents `v1` et `v2`.
2. Ajoutez un transformer de document qui renseigne `Info` (titre, contact Sofia Marques) et déclare le schéma de sécurité `Bearer` ; un transformer d'opération qui ajoute l'exigence Bearer sur les opérations protégées.
3. Créez la **v2** de `GET /{numero}` : la remise est expliquée (`client`, `volume`, `appliquee`, `plafondAtteint`) et les totaux décomposés (`brut`, `remises`, `net`, `pieces`). Le groupe déclare les versions 1.0 et 2.0 ; les deux GET `/{numero}` sont mappés chacun à sa version ; les autres endpoints servent les deux.
4. Faites en sorte que le `Location` d'un POST respecte la version demandée (`/api/v2/commandes/...` pour un POST sur v2).

Point de contrôle 3 : `/openapi/v1.json` et `/openapi/v2.json` répondent ; Scalar affiche les deux documents et le cadenas sur les opérations commandes ; `/api/v3/commandes` renvoie un ProblemDetails (404 : la version n'existe pas dans l'URL).

### Étape 4 — JWT bearer, policies, exploitation (15 min)

1. `AddAuthentication().AddJwtBearer()` avec `MapInboundClaims = false` et `RoleClaimType = "role"` ; `AddAuthorizationBuilder()` avec deux politiques : `commandes.lecture` (rôles `adv`, `entrepot`, `partenaire`) et `commandes.ecriture` (rôle `adv`). Le groupe exige la lecture ; POST, validation et DELETE exigent l'écriture.
2. Créez vos jetons : `dotnet user-jwts create --name sofia.marques --role adv` et `--name marc.vandewalle --role entrepot`. Vérifiez 401 sans jeton, 403 avec Marc sur un POST, 201 avec Sofia.
3. **Health checks** : `/health/live` (aucun check) et `/health/ready` (un `IHealthCheck` qui vérifie que le référentiel articles est chargé).
4. **Rate limiting** : politique `api` à fenêtre fixe, partition par nom d'utilisateur (sinon IP), limite et fenêtre lues dans la configuration via un objet `RateLimitingOptions` validé au démarrage (`ValidateDataAnnotations().ValidateOnStart()`), réponse 429 en ProblemDetails avec `Retry-After`.
5. **OpenTelemetry** : `AddOpenTelemetry()` avec instrumentation ASP.NET Core et HttpClient, une `ActivitySource` métier (`commande.creer`, `commande.valider`), un compteur `trame.commandes.creees`, export console si `Telemetrie:Console` vaut `true`, export OTLP si `OTEL_EXPORTER_OTLP_ENDPOINT` est défini.
6. **CORS** : politique `extranet` avec les origines lues en configuration ; **output cache** de 5 minutes sur un endpoint public `GET /api/v1/referentiel/statuts` (statuts et transitions possibles).

Point de contrôle 4 : en lançant l'API avec `Telemetrie:Console = true`, un POST affiche dans la console une trace `commande.creer` avec le tag `commande.numero` ; `/health/ready` répond `Healthy`.

### Étape 5 — Tests d'intégration avec WebApplicationFactory (15 min)

1. Ajoutez `public partial class Program;` à la fin de `Program.cs`.
2. Écrivez une `TrameApiFactory : WebApplicationFactory<Program>` qui injecte en mémoire la configuration `Authentication:Schemes:Bearer` (issuer, audience, clé de signature base64 de 32 octets), une limite de débit haute et `Telemetrie:Console = false`. Écrivez une fabrique dérivée avec une limite de 3 requêtes pour tester le 429.
3. Écrivez une classe `Jetons` qui fabrique des JWT signés avec la même clé (`JwtSecurityTokenHandler`, claims `sub` et `role`).
4. Écrivez **au moins huit tests** : liste paginée 200 avec `api-supported-versions`, 401 sans jeton, 403 avec le rôle `entrepot` sur un POST, 201 + `Location` + numéro au bon format + remise plafonnée à 30 %, 400 ValidationProblem, 404 ProblemDetails, validation puis 409 au second appel, `EnAttenteStock` sans stock, v2 avec totaux décomposés, version inconnue, OpenAPI v1 et v2 publiés, health checks, 429 au-delà de la limite.

Point de contrôle 5 : `dotnet test` vert, au moins huit tests, aucun test dépendant de l'ordre d'exécution (chaque test crée ses propres commandes).

### Étape 6 — Container et AppHost Aspire (10 min, autonomie)

1. Ajoutez au csproj de l'API les propriétés `PublishContainer` : `ContainerRepository` = `textinord/trame-api`, tags `1.0.0` et `latest`, image de base `mcr.microsoft.com/dotnet/aspnet:10.0`, port 8080, utilisateur `app`. Produisez l'image : avec Docker, `dotnet publish -c Release /t:PublishContainer` ; sans Docker, ajoutez `-p ContainerArchiveOutputPath=./out/trame-api.tar.gz`.
2. Écrivez un `Dockerfile` multi-stage équivalent (SDK pour publier, runtime `aspnet:10.0` pour exécuter, `USER $APP_UID`, port 8080) et un `.dockerignore`.
3. Créez `src/Textinord.Trame.AppHost` (SDK `Aspire.AppHost.Sdk`, package `Aspire.Hosting.AppHost`) qui référence l'API avec `WithHttpHealthCheck("/health/ready")` et `WithExternalHttpEndpoints()`. Ajoutez-le à la solution. Lancez-le et observez une trace dans le dashboard.

Point de contrôle 6 : `dotnet build` de la solution complète sans avertissement ; l'archive d'image ou l'image locale existe ; le dashboard montre la ressource `trame-api` en état Running.

### Étape 7 — Déploiement Azure Container Apps (10 min, lecture et script)

1. Écrivez `infra/deploy-aca.sh` : connexion, groupe de ressources (France Centre), ACR, `dotnet publish /t:PublishContainer -p ContainerRegistry=...`, environnement Container Apps avec Log Analytics, `az containerapp create` (ingress externe 8080, 1 à 3 réplicas, identité managée + rôle AcrPull), sondes de santé, vérification `curl`, et l'option `--teardown`. Chaque commande est commentée : le script se lit, il ne s'exécute pas en salle.
2. Écrivez `infra/README.md` : ce que fait le script, le coût indicatif, et le **plan B local** (API seule + `user-jwts`, AppHost Aspire, archive d'image sans Docker, Docker ou Podman si disponibles), ainsi que ce qui change en production (état hors processus, Entra ID, Key Vault, Azure Monitor, pipeline).

Point de contrôle 7 : un collègue qui n'a pas suivi le module comprend, en lisant `infra/README.md`, comment lancer l'API localement et ce que le script créerait sur Azure.

## Livrable

Un dépôt Git contenant :

- `Textinord.Trame.sln`, `Directory.Build.props`, `.gitignore` (bin, obj, TestResults, out)
- `src/Textinord.Trame.Api` (Minimal API complète), `src/Textinord.Trame.AppHost`
- `tests/Textinord.Trame.Api.Tests` (au moins huit tests verts)
- `Dockerfile`, `.dockerignore`, `infra/deploy-aca.sh`, `infra/README.md`
- Un fichier `docs/adr-api-commandes.md` de dix lignes reprenant les décisions prises : version dans l'URL, codes d'erreur, politique d'autorisation, choix Container Apps

## Dépannage

| Symptôme | Cause probable | Correction |
|---|---|---|
| 401 avec un jeton `user-jwts` valide | l'API a démarré avant la création de la clé, ou l'audience ne correspond pas à `applicationUrl` | redémarrer `dotnet run` ; vérifier `ValidAudiences` dans `appsettings.Development.json` |
| 403 pour Sofia sur un POST | le claim `role` a été renommé par le mapping WS-* | `MapInboundClaims = false` et `RoleClaimType = "role"` |
| `/openapi/v1.json` liste `{version}` comme paramètre | API Explorer non configuré | `AddApiExplorer` avec `SubstituteApiVersionInUrl = true` |
| Le GET v2 renvoie la représentation v1 | les deux endpoints ne sont pas mappés à leur version | `.MapToApiVersion(1.0)` et `.MapToApiVersion(2.0)` sur chaque GET `/{numero}` |
| Les tests ignorent la limite de débit configurée | options lues au démarrage, avant la configuration de test | lire les options via `IOptions<RateLimitingOptions>` (Options pattern), pas `builder.Configuration` |
| Avertissement NU1903 sur `Microsoft.OpenApi` 2.0.0 | dépendance transitive vulnérable | référence explicite à une version corrigée (2.12 ou plus) |
| `PublishContainer` échoue sans Docker | pas de démon pour recevoir l'image | `-p ContainerArchiveOutputPath=./out/trame-api.tar.gz` |
| L'AppHost signale ASPIRE010 | bundle CLI Aspire non embarqué | `<AspireUseCliBundle>false</AspireUseCliBundle>` et `NoWarn` ASPIRE010, ou installer `Aspire.Cli` |

## Grille d'évaluation

| Critère | Points |
|---|---|
| Endpoints versionnés v1 / v2, `Location` correct, pagination plafonnée, filtres | 4 |
| ProblemDetails uniformes (400, 404, 409, 422, 429) avec `type` stable et `code` métier | 3 |
| Endpoint filter de validation générique, réutilisable | 2 |
| OpenAPI par version avec schéma Bearer, Scalar fonctionnel | 2 |
| JWT bearer + politiques ; 401 / 403 / 201 démontrés | 3 |
| Health checks, rate limiting configuré par Options validées, OpenTelemetry avec span métier | 3 |
| Au moins huit tests `WebApplicationFactory` verts et indépendants | 3 |
| Container (Dockerfile **et** `PublishContainer`), AppHost Aspire qui compile | 2 |
| Script `deploy-aca.sh` commenté, `infra/README.md` avec plan B | 2 |
| Qualité : durées de vie correctes, aucun avertissement de build, ADR de dix lignes | 1 |
| **Total** | **25** |

## Teardown

- Supprimer les jetons de développement : `dotnet user-jwts clear` dans le projet API
- Supprimer `out/` (archives d'images), `bin/`, `obj/`, `TestResults/`
- Si le script Azure a été exécuté : `./infra/deploy-aca.sh --teardown` (supprime le groupe de ressources complet)
- Arrêter l'AppHost (Ctrl+C) : il libère les ports 15043 / 17043 et arrête le dashboard
