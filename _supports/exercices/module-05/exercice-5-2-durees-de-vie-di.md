# Exercice 5.2 — Diagnostiquer quatre erreurs de durée de vie

> Module : 5 — Communication, API et Cloud Azure
> Durée estimée : 20 min
> Difficulté : 3 / 5
> Type : Exercice de diagnostic en binôme, lecture de code

## Objectifs pédagogiques

À la fin de cet exercice, vous serez capable de :

- Reconnaître une dépendance captive et un `DbContext` mal enregistré à la lecture d'un `Program.cs`
- Expliquer pourquoi `new HttpClient()` par appel et un scope jamais libéré finissent par faire tomber une API
- Corriger chaque défaut avec l'outil prévu par .NET (`IHttpClientFactory`, `IServiceScopeFactory`, durées de vie, validation au démarrage)

## Prérequis

- Avoir suivi la section 2 du module (injection de dépendances) et regardé le schéma des durées de vie
- Un éditeur pour lire le code ; la compilation n'est pas nécessaire (le code compile, c'est justement le problème)

## Contexte

Un développeur de l'équipe de Sofia a porté un morceau de Trame vers Trame 2 : le service de
tarif, le service de commandes et un client vers l'ancien service stock de Trame (encore en
WCF, appelé en HTTP le temps de la migration). Tout compile, les tests unitaires passent, la
recette a validé. En production, après trois jours :

- la mémoire du container monte de 200 Mo à 1,8 Go puis il redémarre (OOM) ;
- des commandes portent un prix calculé avec la condition tarifaire d'un **autre** client ;
- Kestrel journalise « `Only one usage of each socket address is normally permitted` » ;
- le pool de connexions SQL est épuisé (`The connection pool has been exhausted`) alors que le trafic est faible.

## Énoncé

### Le code fourni

`Program.cs` (extrait) :

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

`TarifService.cs` :

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

`StockLegacyClient.cs` :

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

`CommandesService.cs` :

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

### Partie 1 — Les quatre défauts (8 min)

Identifiez les quatre erreurs de durée de vie ou d'usage du conteneur. Pour chacune, remplissez :

| Nº | Où (fichier, ligne) | Nature du défaut | Symptôme de production qu'il explique |
|---|---|---|---|
| 1 | | | |
| 2 | | | |
| 3 | | | |
| 4 | | | |

Un des quatre symptômes listés dans le contexte correspond à chaque défaut : reliez-les.

### Partie 2 — Les corrections (8 min)

Pour chaque défaut, écrivez la correction : la ligne de `Program.cs` à changer et/ou le
code de la classe corrigée. Contraintes :

- le `DbContext` reste un `DbContext` EF Core (ne le remplacez pas par autre chose) ;
- `TarifService` doit rester utilisable depuis un singleton si un jour un `BackgroundService` en a besoin (réfléchissez à ce qu'il doit ou ne doit pas contenir) ;
- l'appel au service stock legacy doit survivre à 15 000 appels par jour sans épuiser les sockets ;
- `CommandesService` ne doit plus créer de scope lui-même.

### Partie 3 — Le filet de sécurité (4 min)

Quelle configuration du conteneur aurait fait échouer le démarrage de l'application sur le
défaut nº 1 avant même la recette ? Où s'active-t-elle, et pourquoi ne s'était-elle pas
déclenchée ici alors que l'équipe développe en `Development` ? Répondez en cinq lignes.

## Indices (à consulter si bloqué)

<details>
<summary>Indice 1 — Par où commencer</summary>

Dessinez pour chaque classe sa durée de vie enregistrée, puis celle de chacune de ses
dépendances. Toute flèche qui va d'une durée longue vers une durée plus courte est suspecte.
Un champ privé qui garde une valeur entre deux appels d'un singleton l'est aussi.

</details>

<details>
<summary>Indice 2 — Le prix d'un autre client</summary>

`_conditionCourante ??=` mémorise la première condition rencontrée. Dans un singleton, la
première requête gagne pour toute la vie du processus : les 1 199 autres clients héritent de
son taux. Ce défaut est indépendant du conteneur, mais c'est le singleton qui le rend
catastrophique.

</details>

<details>
<summary>Indice 3 — Le scope</summary>

`CreateScope()` retourne un `IServiceScope` jetable ; sans `using`, le scope et tout ce qu'il
a créé (repository, DbContext, connexion) vivent jusqu'au ramasse-miettes, parfois plus
longtemps que la connexion SQL ne le tolère. Mais la vraie question est : pourquoi un service
déjà scoped a-t-il besoin de créer un scope ?

</details>

## Pour aller plus loin (bonus)

Écrivez le test xUnit qui aurait attrapé le défaut nº 1 sans base de données : construire la
`ServiceCollection` de `Program.cs` avec `ValidateScopes = true` et `ValidateOnBuild = true`,
puis vérifier qu'un `InvalidOperationException` est levé au `BuildServiceProvider()`.
