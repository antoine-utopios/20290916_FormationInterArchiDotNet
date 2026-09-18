# Solution — Exercice 5.2 : Diagnostiquer quatre erreurs de durée de vie

> Document formateur — Ne pas distribuer avant la correction collective.

## Approche pédagogique

Le code fourni compile et démarre : c'est le point à marteler. Le conteneur de .NET ne
protège pas de tout, et les défauts de durée de vie ne se voient qu'en charge ou dans la durée.
Faites d'abord dessiner les flèches de dépendance avec leur durée de vie (singleton →
scoped = suspect), puis reliez chaque défaut à un symptôme. La correction du défaut 1 est
l'occasion de montrer `ValidateScopes` en direct : lancer l'application en `Development` avec
un `DbContext` scoped et un `TarifService` singleton fait échouer `builder.Build()` avec un
message explicite, ce qui est bien plus convaincant qu'une explication.

## Solution détaillée

### Partie 1 — Les quatre défauts

| Nº | Où | Nature du défaut | Symptôme de production |
|---|---|---|---|
| 1 | `Program.cs` : `AddSingleton<TarifService>()` alors que `TarifService` reçoit `ICommandesRepository` (scoped) et `TrameDbContext` | **Dépendance captive** : un singleton retient des services scoped pour toute la vie du processus ; s'y ajoute un champ `_conditionCourante` qui mémorise la condition du premier client | prix calculés avec la condition tarifaire d'un autre client (la première requête fixe le taux pour tous) |
| 2 | `Program.cs` : `AddDbContext<TrameDbContext>(…, ServiceLifetime.Singleton)` | `DbContext` **singleton** : une seule instance partagée par toutes les requêtes, non thread-safe, change tracker jamais vidé | mémoire qui monte à 1,8 Go puis OOM (le tracker garde toutes les entités lues) ; erreurs de concurrence intermittentes |
| 3 | `StockLegacyClient.StockDisponibleAsync` : `new HttpClient { … }` à chaque appel, jamais disposé | **`HttpClient` par appel** : chaque instance ouvre son handler et sa connexion ; 15 000 appels par jour laissent des sockets en `TIME_WAIT` | « Only one usage of each socket address is normally permitted » : épuisement des ports éphémères |
| 4 | `CommandesService.Creer` : `services.CreateScope()` sans `using`, résolution manuelle du repository | **Scope créé et jamais libéré** (et service locator inutile : `CommandesService` est déjà scoped, il suffit d'injecter le repository) | pool de connexions SQL épuisé : chaque scope orphelin garde son `DbContext` et sa connexion jusqu'au ramasse-miettes |

Le défaut 1 masque en partie le défaut 2 : comme `TrameDbContext` est enregistré singleton,
le conteneur ne détecte pas la captive au démarrage (un singleton qui dépend d'un singleton
est légal). C'est en corrigeant le 2 (scoped) que le 1 devient détectable par
`ValidateScopes`. Les apprenants qui trouvent ce lien ont compris l'exercice.

### Partie 2 — Les corrections

#### `Program.cs` — avant

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TrameDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("Trame")), ServiceLifetime.Singleton);
builder.Services.AddSingleton<TarifService>();
builder.Services.AddScoped<CommandesService>();
builder.Services.AddSingleton<StockLegacyClient>();
builder.Services.AddScoped<ICommandesRepository, CommandesRepository>();

var app = builder.Build();
app.MapPost("/api/v1/commandes", (CreerCommandeRequete requete, CommandesService service) => service.Creer(requete));
app.Run();
```

#### `Program.cs` — après

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;

var builder = WebApplication.CreateBuilder(args);

// Défaut 2 : le DbContext est scoped (durée de vie par défaut de AddDbContext) : une instance par requête,
// un change tracker qui meurt avec la requête, une connexion rendue au pool à la fin du scope.
builder.Services.AddDbContext<TrameDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Trame")));

// Défaut 1 : TarifService ne retient plus rien ; il devient scoped comme ce dont il dépend.
// (S'il devait rester singleton pour un BackgroundService, il recevrait IServiceScopeFactory
//  et créerait un scope par calcul : voir la variante plus bas.)
builder.Services.AddScoped<TarifService>();
builder.Services.AddScoped<CommandesService>();
builder.Services.AddScoped<ICommandesRepository, CommandesRepository>();

// Défaut 3 : client HTTP typé, handlers poolés par IHttpClientFactory, résilience standard
// (timeout, retry, circuit breaker) fournie par Microsoft.Extensions.Http.Resilience.
builder.Services.AddOptions<StockLegacyOptions>()
    .Bind(builder.Configuration.GetSection("Trame:StockLegacy"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<StockLegacyClient>((services, client) =>
    {
        var options = services.GetRequiredService<IOptions<StockLegacyOptions>>().Value;
        client.BaseAddress = options.Url;
        client.Timeout = TimeSpan.FromSeconds(5);
    })
    .AddStandardResilienceHandler();

var app = builder.Build();

app.MapPost("/api/v1/commandes", async (CreerCommandeRequete requete, CommandesService service) =>
    TypedResults.Ok(await service.CreerAsync(requete)));

app.Run();
```

`StockLegacyOptions` (nouveau fichier, Options pattern à la place de `IConfiguration` dans un service) :

```csharp
using System.ComponentModel.DataAnnotations;

public sealed class StockLegacyOptions
{
    [Required]
    public Uri Url { get; set; } = new("https://trame.textinord.local/stock/");
}
```

#### `TarifService.cs` — avant

```csharp
public sealed class TarifService(ICommandesRepository repository, TrameDbContext db)
{
    private ConditionTarifaire? _conditionCourante;

    public decimal Calculer(string codeClient, int quantite, decimal prixBase)
    {
        _conditionCourante ??= db.Clients.Single(c => c.Code == codeClient).ConditionTarifaire;
        var remise = CalculRemise.Calculer(_conditionCourante.TauxRemise, quantite);
        return prixBase * (1 - remise.TauxApplique);
    }
}
```

#### `TarifService.cs` — après

```csharp
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Scoped : une instance par requête, aucun état entre deux appels. Le repository inutilisé
/// a disparu de la signature ; la condition tarifaire est lue à chaque calcul (une requête
/// indexée, en no-tracking) et pourra être mise en cache par client au module 4 si besoin.
/// </summary>
public sealed class TarifService(TrameDbContext db)
{
    public async Task<decimal> CalculerAsync(string codeClient, int quantite, decimal prixBase, CancellationToken ct = default)
    {
        var condition = await db.Clients
            .AsNoTracking()
            .Where(c => c.Code == codeClient)
            .Select(c => c.ConditionTarifaire)
            .SingleAsync(ct);

        var remise = CalculRemise.Calculer(condition.TauxRemise, quantite);
        return prixBase * (1 - remise.TauxApplique);
    }
}
```

Variante si `TarifService` doit rester singleton (appelé par un `BackgroundService` de
recalcul nocturne) : il ne reçoit ni `DbContext` ni repository, mais `IServiceScopeFactory`,
et crée un scope borné par calcul.

```csharp
public sealed class TarifServiceSingleton(IServiceScopeFactory scopes)
{
    public async Task<decimal> CalculerAsync(string codeClient, int quantite, decimal prixBase, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrameDbContext>();

        var condition = await db.Clients
            .AsNoTracking()
            .Where(c => c.Code == codeClient)
            .Select(c => c.ConditionTarifaire)
            .SingleAsync(ct);

        var remise = CalculRemise.Calculer(condition.TauxRemise, quantite);
        return prixBase * (1 - remise.TauxApplique);
    }
}
```

#### `StockLegacyClient.cs` — avant

```csharp
public sealed class StockLegacyClient(IConfiguration configuration)
{
    public async Task<int> StockDisponibleAsync(string reference, string entrepot)
    {
        var client = new HttpClient { BaseAddress = new Uri(configuration["Trame:StockUrl"]!) };
        var reponse = await client.GetFromJsonAsync<StockReponse>($"stock/{entrepot}/{reference}");
        return reponse?.Disponible ?? 0;
    }
}

public sealed record StockReponse(int Disponible);
```

#### `StockLegacyClient.cs` — après

```csharp
using System.Net.Http.Json;

/// <summary>
/// Client typé : l'instance HttpClient est fournie par IHttpClientFactory (handlers poolés,
/// DNS rafraîchi, résilience). La classe elle-même est transient, ce qui est sans importance :
/// elle ne possède rien.
/// </summary>
public sealed class StockLegacyClient(HttpClient http)
{
    public async Task<int> StockDisponibleAsync(string reference, string entrepot, CancellationToken ct = default)
    {
        var reponse = await http.GetFromJsonAsync<StockReponse>($"stock/{entrepot}/{reference}", ct);
        return reponse?.Disponible ?? 0;
    }
}

public sealed record StockReponse(int Disponible);
```

#### `CommandesService.cs` — avant

```csharp
public sealed class CommandesService(
    IServiceProvider services,
    TarifService tarif,
    StockLegacyClient stock,
    ILogger<CommandesService> logger)
{
    public async Task<CommandeDetail> Creer(CreerCommandeRequete requete)
    {
        var scope = services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICommandesRepository>();

        var commande = new Commande(requete.CodeClient);
        foreach (var ligne in requete.Lignes)
        {
            var disponible = await stock.StockDisponibleAsync(ligne.Reference, "RBX");
            var prix = tarif.Calculer(requete.CodeClient, ligne.Quantite, ligne.PrixBase);
            commande.AjouterLigne(ligne.Reference, ligne.Quantite, prix, disponible);
        }

        await repository.AjouterAsync(commande);
        logger.LogInformation("Commande {Numero} créée", commande.Numero);
        return commande.VersDetail();
    }
}
```

#### `CommandesService.cs` — après

```csharp
/// <summary>
/// Scoped : ses dépendances sont scoped ou sans état. Plus de IServiceProvider, plus de scope
/// manuel : le repository est injecté par le constructeur et vit exactement le temps de la requête.
/// </summary>
public sealed class CommandesService(
    ICommandesRepository repository,
    TarifService tarif,
    StockLegacyClient stock,
    ILogger<CommandesService> logger)
{
    public async Task<CommandeDetail> CreerAsync(CreerCommandeRequete requete, CancellationToken ct = default)
    {
        var commande = new Commande(requete.CodeClient);

        foreach (var ligne in requete.Lignes)
        {
            var disponible = await stock.StockDisponibleAsync(ligne.Reference, "RBX", ct);
            var prix = await tarif.CalculerAsync(requete.CodeClient, ligne.Quantite, ligne.PrixBase, ct);
            commande.AjouterLigne(ligne.Reference, ligne.Quantite, prix, disponible);
        }

        await repository.AjouterAsync(commande, ct);
        logger.LogInformation("Commande {Numero} créée", commande.Numero);
        return commande.VersDetail();
    }
}
```

### Partie 3 — Le filet de sécurité

`ServiceProviderOptions.ValidateScopes` (refuse de résoudre un scoped depuis le fournisseur
racine, donc depuis un singleton) et `ValidateOnBuild` (construit chaque enregistrement au
démarrage pour vérifier ses dépendances). `WebApplication.CreateBuilder` les active
automatiquement quand l'environnement est `Development` — pas en `Production`. Ici, ils ne se
sont pas déclenchés en développement parce que le `DbContext` était lui-même enregistré en
singleton (défaut 2) et que `ICommandesRepository`, bien que scoped, n'était résolu par
`TarifService` qu'à travers un singleton déjà construit… avant que quiconque ne le remarque :
`TarifService` est construit à la première requête, hors validation `OnBuild` si le service
n'est pas résolu au démarrage. Corriger le défaut 2 suffit à faire éclater le défaut 1 au
premier `dotnet run` en développement, et à le rendre visible dans le test du bonus. En
production, on peut forcer la validation quel que soit l'environnement :

```csharp
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});
```

## Variantes acceptables

1. Garder `TarifService` singleton avec `IServiceScopeFactory` (variante ci-dessus) : correct
   si l'apprenant justifie le besoin (service partagé par un worker) et supprime le champ
   `_conditionCourante`.
2. Un cache de conditions tarifaires (`IMemoryCache`, clé = code client, expiration 10 min)
   dans `TarifService` : correct et pertinent (1 200 clients, conditions qui changent rarement),
   à condition que le cache soit par client et non « le premier client ».
3. `IHttpClientFactory.CreateClient("stock")` (client nommé) à la place du client typé :
   correct, un peu moins lisible.
4. `PooledDbContextFactory` pour un singleton qui a besoin du `DbContext` : correct,
   c'est l'outil EF Core prévu pour ce cas (module 4).

## Erreurs classiques à repérer en correction

| Erreur observée | Cause probable | Comment corriger |
|---|---|---|
| Passer `TarifService` en transient « pour être sûr » | confusion : transient n'empêche pas une captive si le consommateur est singleton | raisonner sur le consommateur le plus long, pas sur le service lui-même |
| Ajouter `using` devant `CreateScope()` et s'arrêter là | le symptôme est corrigé, pas la cause | supprimer le scope : un service scoped injecte directement ses dépendances scoped |
| `HttpClient` en singleton via `AddSingleton(new HttpClient())` | corrige les sockets mais fige le DNS et perd la résilience | `AddHttpClient<T>()` : c'est le rôle de la fabrique |
| `DbContext` en transient | fonctionne mais casse l'unité de travail par requête (deux instances dans la même requête ne partagent rien) | scoped, la valeur par défaut |

## Points à insister en débriefing

- La règle tient en une phrase : un service ne dépend que de services de durée de vie égale ou
  plus longue. Tout le reste en découle.
- Le conteneur valide en `Development` par défaut ; une dépendance captive qui arrive en
  production a donc contourné quelque chose : un singleton résolu tard, un `DbContext` mal
  enregistré, un environnement de test en `Production`.
- `IHttpClientFactory`, `IServiceScopeFactory`, `IOptions<T>` : trois abstractions fournies par
  .NET précisément pour éviter d'écrire `new` au mauvais endroit.

## Bonus

Test qui attrape le défaut 1 sans base de données (le `DbContext` est remplacé par un
enregistrement scoped quelconque : ce qui compte est la durée de vie, pas l'implémentation) :

```csharp
using Microsoft.Extensions.DependencyInjection;

public sealed class DureesDeVieTests
{
    [Fact]
    public void Un_singleton_qui_depend_d_un_scoped_est_refuse_au_demarrage()
    {
        var services = new ServiceCollection();
        services.AddScoped<TrameDbContext>();
        services.AddScoped<ICommandesRepository, CommandesRepository>();
        services.AddSingleton<TarifServiceAvant>(); // la version fautive de l'exercice

        var exception = Assert.Throws<AggregateException>(() =>
            services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true,
            }));

        Assert.Contains("Cannot consume scoped service", exception.ToString());
    }
}
```

`ValidateOnBuild` lève une `AggregateException` qui regroupe toutes les erreurs de
construction ; le message interne cite le service scoped consommé par le singleton. Sans
`ValidateOnBuild`, l'erreur n'apparaîtrait qu'à la première résolution de `TarifServiceAvant`.
