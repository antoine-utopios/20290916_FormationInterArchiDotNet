# Solution — Exercice 2.2 : nommage et structure d'une solution

Document formateur.

## Partie 1 — La structure

### Arborescence cible

```text
Textinord.Trame.sln
Directory.Build.props          propriétés communes, analyzers, section conditionnelle *.Tests
Directory.Packages.props       une version par package (Central Package Management)
.editorconfig                  style et règles de nommage
global.json                    bande de SDK 10.0.x
.gitignore
src/
  Textinord.Trame.Domain/            AssemblyName = RootNamespace = Textinord.Trame.Domain
  Textinord.Trame.Application/
  Textinord.Trame.Infrastructure/
  Textinord.Trame.Api/
  Textinord.Trame.Worker/            (facultatif à ce stade, mais prévu par l'ADR)
tests/
  Textinord.Trame.Domain.Tests/
  Textinord.Trame.Application.Tests/
  Textinord.Trame.Api.Tests/         (si l'on garde des tests d'API)
tools/
docs/
```

### Où va le contenu livré

| Projet livré | Destination |
|---|---|
| `Core` (assembly `core`, namespace `Textinord`) | `Textinord.Trame.Domain` : mêmes types, assembly et namespace racine alignés sur le nom du projet |
| `TextinordTrameBLL` — règles métier | `Textinord.Trame.Domain` (règles pures) et `Textinord.Trame.Application` (cas d'usage, orchestration) |
| `TextinordTrameBLL` — accès EF Core | `Textinord.Trame.Infrastructure` ; l'interface du dépôt reste dans `Application` (ou `Domain` si c'est un port du domaine) |
| `TextinordTrameBLL` — envoi des messages | abstraction `IMessageBus` dans `Application`, implémentation dans `Infrastructure` |
| `DAL` | `Textinord.Trame.Infrastructure` |
| `WebApi` (`TrameApi`, namespace `trameapi.controllers`) | `Textinord.Trame.Api`, namespace `Textinord.Trame.Api.<Fonctionnalité>` |
| `Tests` (`UnitTests`, référence `TrameApi`) | éclaté : `Domain.Tests`, `Application.Tests`, chacun ne référence que le projet testé ; les tests d'API dans `Api.Tests` avec `WebApplicationFactory` (module 5) |
| `Utils` — extensions LINQ, helpers de dates | soit dans le projet qui les utilise, soit `Textinord.Trame.Domain.Commun` s'ils sont métier ; pas de projet fourre-tout |
| `Utils` — client HTTP du WMS | `Textinord.Trame.Infrastructure.Entrepots` : c'est un adaptateur, derrière `IEntrepotGateway` déclaré dans `Application` |

### Références autorisées et erreurs livrées

Autorisées : `Application → Domain` ; `Infrastructure → Application` (et donc Domain) ; `Api` et `Worker →
Application + Infrastructure` (composition uniquement) ; `X.Tests → X`.

Les deux références livrées dans le mauvais sens :

1. `DAL → TextinordTrameBLL` : la couche d'accès aux données dépend de la couche métier, qui contient elle-même
   de l'accès aux données. Dans la cible, c'est `Infrastructure → Application`, jamais l'inverse, et le métier ne
   connaît que des interfaces.
2. `Tests → TrameApi` uniquement : les tests unitaires du métier passent par l'API, donc par tout le graphe. Chaque
   projet de tests référence le projet qu'il teste, et rien d'autre.

### Fichiers de racine à ajouter

`Directory.Build.props` (propriétés communes, analyzers, `TreatWarningsAsErrors`), `Directory.Packages.props`
(fin des versions divergentes : `xunit` 2.4.2 contre 2.9.3, `Microsoft.Extensions.Logging` 8 contre 10),
`.editorconfig` (règles appliquées à la compilation avec `EnforceCodeStyleInBuild`), `global.json` (tous les
postes et la CI sur la même bande de SDK), `.gitignore`.

## Partie 2 — Les noms

| Nom livré | Règle violée | Nom corrigé |
|---|---|---|
| `namespace TextinordTrameBLL` | namespace = `Societe.Produit.Couche`, PascalCase avec points ; « BLL » est une abréviation technique | `Textinord.Trame.Application.Commandes` |
| `interface commandeService` | préfixe `I` obligatoire, PascalCase | `ICommandeService` (ou mieux : `IValidationCommande`) |
| `Task<Commande> Validate(string num)` (interface et classe, `async`) | méthode asynchrone sans suffixe `Async` ; paramètre abrégé | `Task<Commande> ValiderAsync(string numero)` |
| `void send_to_entrepot(Commande c)` | snake_case ; paramètre d'une lettre | `void EnvoyerAEntrepot(Commande commande)` — et, puisqu'elle appelle une méthode async, `Task EnvoyerAEntrepotAsync(...)` |
| `class CommandeMgr` | abréviation (`Mgr`) ; « Manager » est de toute façon un nom creux | `ValidationCommandeService` ou `CommandeService` |
| `private readonly ICommandeRepo m_repo` | préfixe `m_` ; abréviation `Repo` | `private readonly ICommandeRepository _commandes` |
| `private readonly IEntrepotGW _EntrepotGw` | champ privé en PascalCase après `_` ; abréviation `GW` | `private readonly IEntrepotGateway _entrepots` |
| `private int iCount` | notation hongroise ; anglais dans un code français | `private int _nombreValidations` |
| `public const int MAX_LIGNES` | SCREAMING_CASE ; les constantes sont en PascalCase | `public const int NombreMaximalDeLignes` |
| `public string strDernierNumero` | notation hongroise ; champ public (devrait être une propriété) | `public string? DernierNumero { get; private set; }` |
| `string strNum` (paramètre) | notation hongroise, abréviation | `string numero` |
| `var cmd = ...` | abréviation | `var commande = ...` |
| `public string GetAdresseLivraisonAsync(Client c)` | suffixe `Async` sur une méthode synchrone ; « Get » en anglais devant un nom français ; paramètre d'une lettre | `public string AdresseLivraison(Client client)` |
| `Task<bool> CheckStock(Commande c)` (`async`) | méthode asynchrone sans `Async` ; anglais et français mélangés | `Task<bool> VerifierStockAsync(Commande commande)` |
| `interface ICommandeRepo` / `Task<Commande> GetByNum(string num)` / `Task<bool> HasStock(...)` | abréviations ; méthodes retournant `Task` sans suffixe `Async` | `ICommandeRepository`, `TrouverParNumeroAsync`, `ADuStockAsync` |
| `interface IEntrepotGW` | abréviation | `IEntrepotGateway` |
| `using System;` avec `ImplicitUsings` | style : usings superflus (IDE0005) | supprimer |
| `namespace ... { }` sur bloc | style du dépôt : namespace file-scoped (IDE0161) | `namespace Textinord.Trame.Application.Commandes;` |

Dix-neuf corrections ; douze suffisent pour valider l'exercice. Les défauts de conception que l'on ne corrige
pas ici mais que l'on cite : `.Wait()` sur une tâche (blocage, deadlock possible), `throw new Exception`
générique, champ public mutable, compteur non thread-safe.

### Extrait corrigé

```csharp
namespace Textinord.Trame.Application.Commandes;

public interface ICommandeService
{
    Task<Commande> ValiderAsync(string numero, CancellationToken cancellationToken);

    Task EnvoyerAEntrepotAsync(Commande commande, CancellationToken cancellationToken);
}

public sealed class CommandeService(ICommandeRepository commandes, IEntrepotGateway entrepots) : ICommandeService
{
    public const int NombreMaximalDeLignes = 200;

    private int _nombreValidations;

    public string? DernierNumero { get; private set; }

    public async Task<Commande> ValiderAsync(string numero, CancellationToken cancellationToken)
    {
        var commande = await commandes.TrouverParNumeroAsync(numero, cancellationToken);

        if (commande.Lignes.Count > NombreMaximalDeLignes)
        {
            throw new InvalidOperationException($"La commande {numero} dépasse {NombreMaximalDeLignes} lignes.");
        }

        Interlocked.Increment(ref _nombreValidations);
        DernierNumero = numero;
        return commande;
    }

    public Task EnvoyerAEntrepotAsync(Commande commande, CancellationToken cancellationToken) =>
        entrepots.EnvoyerAsync(commande, cancellationToken);

    public static string AdresseLivraison(Client client) => client.Adresse;

    public Task<bool> VerifierStockAsync(Commande commande, CancellationToken cancellationToken) =>
        commandes.ADuStockAsync(commande, cancellationToken);
}

public interface ICommandeRepository
{
    Task<Commande> TrouverParNumeroAsync(string numero, CancellationToken cancellationToken);

    Task<bool> ADuStockAsync(Commande commande, CancellationToken cancellationToken);
}

public interface IEntrepotGateway
{
    Task EnvoyerAsync(Commande commande, CancellationToken cancellationToken);
}
```

## Partie 3 — La règle `.editorconfig`

La violation la plus fréquente de l'extrait est la méthode asynchrone sans suffixe `Async` (quatre occurrences :
`Validate` deux fois, `CheckStock`, `GetByNum` et `HasStock` côté dépôt). Règle :

```ini
[*.cs]
dotnet_naming_rule.async_methods_end_with_async.symbols = async_methods
dotnet_naming_rule.async_methods_end_with_async.style = suffix_async
dotnet_naming_rule.async_methods_end_with_async.severity = warning

dotnet_naming_symbols.async_methods.applicable_kinds = method
dotnet_naming_symbols.async_methods.required_modifiers = async

dotnet_naming_style.suffix_async.required_suffix = Async
dotnet_naming_style.suffix_async.capitalization = pascal_case

# Indispensable pour la ligne de commande : sans cette sévérité explicite, `dotnet build`
# ne remonte pas IDE1006 (seul l'IDE lit la sévérité écrite dans dotnet_naming_rule).
dotnet_diagnostic.IDE1006.severity = warning
```

Vérifié sur la solution du TP 2 : sans la ligne `dotnet_diagnostic.IDE1006.severity`, une interface
`messageBus` ne déclenche que `CA1715` (analyseur .NET, niveau recommandé) et un champ `Compteur` ou une méthode
`async Charger()` passent la compilation ; avec la ligne, les trois violations sont des erreurs.

Limite à signaler : `required_modifiers = async` ne voit que les méthodes déclarées `async` ; une méthode qui
retourne un `Task` sans le mot-clé (`GetByNum` dans l'interface) n'est pas détectée. Pour ces cas, l'analyseur
`VSTHRD200` (package `Microsoft.VisualStudio.Threading.Analyzers`) vérifie le suffixe sur le type de retour.

La propriété MSBuild qui transforme l'avertissement en échec : `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`,
à condition que `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>` soit aussi présent (sinon les règles
IDE ne sont évaluées que dans l'éditeur, pas par `dotnet build`). Les deux sont dans le `Directory.Build.props`
de la solution `solutions/module-02/tp-2/`.

Deuxième règle la plus fréquente, si un participant la choisit : le préfixe `I` des interfaces
(`commandeService`), même structure avec `applicable_kinds = interface` et `required_prefix = I`.
