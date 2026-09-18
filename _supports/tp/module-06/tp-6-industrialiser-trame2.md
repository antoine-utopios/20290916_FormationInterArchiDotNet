# TP 6 — Industrialiser Trame 2

> Module : 6 — Legacy, messaging, identité d'entreprise et industrialisation
> Durée estimée : 90 min (en séance : étapes 1 à 4 prioritaires, étapes 5 à 7 en autonomie)
> Difficulté : 4 / 5
> Type : TP de synthèse, en binôme ou individuel, sur poste

## Mise en situation

Vendredi 4 septembre 2026, 17 h 40 : le serveur de l'entrepôt de Lesquin a redémarré pour une mise à jour Windows. Douze ordres de préparation attendaient dans la file MSMQ locale, non transactionnelle. Ils ont disparu. Marc Vandewalle l'a découvert lundi matin en recevant les appels de trois clients. Nadia Benali a tranché : la première release de Trame 2 en production, la **2.1**, doit garantir qu'aucune commande validée ne reste sans ordre de préparation, et qu'elle soit livrée par un pipeline, pas par une copie de fichiers.

Sofia Marques vous confie la partie technique. Awa Diop veut un `CHANGELOG` qu'elle peut lire aux entrepôts. Vous construisez :

- une API `POST /commandes` qui enregistre la commande **et** l'événement `CommandeValidee` dans une seule transaction (table Outbox) ;
- un Worker qui relaie l'Outbox vers le bus (MassTransit, transport en mémoire, RabbitMQ activable) et un consumer idempotent qui crée les ordres de préparation ;
- des tests unitaires et d'intégration, un pipeline Azure DevOps et un workflow GitHub Actions, le versioning SemVer et les notes de release.

## Prérequis

- SDK .NET 10 (`dotnet --version` → `10.0.3xx`), un éditeur, `curl` (ou l'interface OpenAPI dans le navigateur)
- Ni Docker ni RabbitMQ ne sont nécessaires : le transport MassTransit en mémoire et SQLite suffisent. Si Docker est disponible, RabbitMQ s'active par configuration à l'étape 3
- Avoir suivi la démo 6.1 ; le code montré en démo est votre référence, pas un copier-coller : chaque étape ci-dessous vous demande de l'écrire et de le comprendre

## Point de départ

Le TP est autonome : le squelette se génère avec `dotnet new`, et le noyau métier minimal vous est fourni ci-dessous (c'est un extrait du `Domain` construit au module 1, réduit à ce dont le TP a besoin).

### Squelette

```bash
mkdir trame2 && cd trame2
dotnet new sln -n Textinord.Trame2 --format sln
dotnet new classlib -n Textinord.Trame.Domain         -o src/Textinord.Trame.Domain
dotnet new classlib -n Textinord.Trame.Infrastructure -o src/Textinord.Trame.Infrastructure
dotnet new web      -n Textinord.Trame.Api            -o src/Textinord.Trame.Api
dotnet new worker   -n Textinord.Trame.Worker         -o src/Textinord.Trame.Worker
dotnet new xunit    -n Textinord.Trame.Domain.Tests   -o tests/Textinord.Trame.Domain.Tests
dotnet new xunit    -n Textinord.Trame.Worker.Tests   -o tests/Textinord.Trame.Worker.Tests
dotnet new xunit    -n Textinord.Trame.Api.Tests      -o tests/Textinord.Trame.Api.Tests
for p in src/*/*.csproj tests/*/*.csproj; do dotnet sln add "$p"; done
dotnet add src/Textinord.Trame.Infrastructure reference src/Textinord.Trame.Domain
dotnet add src/Textinord.Trame.Api            reference src/Textinord.Trame.Infrastructure
dotnet add src/Textinord.Trame.Worker         reference src/Textinord.Trame.Infrastructure
dotnet add tests/Textinord.Trame.Domain.Tests reference src/Textinord.Trame.Domain
dotnet add tests/Textinord.Trame.Worker.Tests reference src/Textinord.Trame.Worker
dotnet add tests/Textinord.Trame.Api.Tests    reference src/Textinord.Trame.Api
```

Supprimez les `Class1.cs`, `UnitTest1.cs` et `Worker.cs` générés. Remplacez chaque `.csproj` par une version sans `TargetFramework` ni `Nullable` (ils viennent des fichiers communs ci-dessous) et sans attribut `Version` sur les packages.

`Directory.Build.props` (racine) :

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Company>Textinord</Company>
    <Product>Trame 2</Product>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
</Project>
```

`Directory.Packages.props` (racine) — les versions valides au 7 septembre 2026 :

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.10" />
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="10.0.10" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.10" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.10" />
    <PackageVersion Include="MassTransit" Version="8.5.10" />
    <PackageVersion Include="MassTransit.RabbitMQ" Version="8.5.10" />
    <PackageVersion Include="Nerdbank.GitVersioning" Version="3.10.94" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.4" />
    <PackageVersion Include="coverlet.collector" Version="6.0.4" />
    <PackageVersion Include="NSubstitute" Version="6.2.0" />
    <PackageVersion Include="SQLitePCLRaw.bundle_e_sqlite3" Version="3.0.5" />
    <PackageVersion Include="Microsoft.OpenApi" Version="2.12.2" />
  </ItemGroup>
</Project>
```

Les deux dernières lignes existent parce que l'**audit NuGet** signale des dépendances transitives vulnérables (`NU1903`) et que `TreatWarningsAsErrors` transforme ce signal en erreur de build : on épingle une version corrigée, c'est le comportement voulu d'une usine logicielle.

`tests/Directory.Build.props` :

```xml
<Project>
  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="NSubstitute" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="NSubstitute" />
  </ItemGroup>
</Project>
```

Références de packages par projet (sans version) : `Infrastructure` → `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.Extensions.Hosting`, `SQLitePCLRaw.bundle_e_sqlite3` ; `Api` → `Microsoft.AspNetCore.OpenApi`, `Microsoft.OpenApi` ; `Worker` → `MassTransit`, `MassTransit.RabbitMQ`, `Microsoft.Extensions.Hosting` ; `Api.Tests` → `Microsoft.AspNetCore.Mvc.Testing`.

### Noyau métier fourni (`src/Textinord.Trame.Domain/`)

```csharp
// StatutCommande.cs
namespace Textinord.Trame.Domain;

public enum StatutCommande { Brouillon, Validee, EnAttenteStock, EnPreparation, Expediee, Facturee, Annulee }
```

```csharp
// RegleRemise.cs
namespace Textinord.Trame.Domain;

public static class RegleRemise
{
    public static decimal Calculer(decimal tauxClient, int quantite)
    {
        if (tauxClient is < 0 or > 0.25m)
        {
            throw new ArgumentOutOfRangeException(nameof(tauxClient), tauxClient, "Le taux client est compris entre 0 et 25 %.");
        }

        return Math.Min(tauxClient + (quantite > 500 ? 0.05m : 0m), 0.30m);
    }
}
```

```csharp
// NumeroCommande.cs
using System.Text.RegularExpressions;

namespace Textinord.Trame.Domain;

public static partial class NumeroCommande
{
    public static string Formater(int annee, int sequence) => $"CMD-{annee:D4}-{sequence:D6}";

    public static bool EstValide(string numero) => Format().IsMatch(numero);

    [GeneratedRegex(@"^CMD-\d{4}-\d{6}$")]
    private static partial Regex Format();
}
```

```csharp
// Resultat.cs
namespace Textinord.Trame.Domain;

public sealed class Resultat<T>
{
    private Resultat(T? valeur, IReadOnlyList<string> erreurs)
    {
        Valeur = valeur;
        Erreurs = erreurs;
    }

    public T? Valeur { get; }

    public IReadOnlyList<string> Erreurs { get; }

    public bool EstSucces => Erreurs.Count == 0;

    public static Resultat<T> Succes(T valeur) => new(valeur, []);

    public static Resultat<T> Echec(params string[] erreurs) => new(default, erreurs);
}
```

```csharp
// Referentiel.cs — Client, Article, Entrepots, StockArticle
namespace Textinord.Trame.Domain;

public sealed class Client
{
    public required string Code { get; init; }
    public required string RaisonSociale { get; init; }
    public decimal TauxRemise { get; init; }
    public bool Actif { get; init; } = true;
}

public sealed class Article
{
    public required string Reference { get; init; }
    public required string Libelle { get; init; }
    public required string Famille { get; init; }
    public decimal PrixBase { get; init; }
}

public static class Entrepots
{
    public const string Roubaix = "RBX";
    public const string Lesquin = "LSQ";
}

public sealed class StockArticle
{
    public required string Reference { get; init; }
    public required string Entrepot { get; init; }
    public int Quantite { get; set; }
}
```

```csharp
// LigneCommande.cs
namespace Textinord.Trame.Domain;

public sealed class LigneCommande
{
    private LigneCommande()
    {
    }

    public LigneCommande(string referenceArticle, int quantite, decimal prixUnitaire, decimal tauxRemiseClient)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceArticle);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantite);
        ArgumentOutOfRangeException.ThrowIfNegative(prixUnitaire);
        ReferenceArticle = referenceArticle;
        Quantite = quantite;
        PrixUnitaire = prixUnitaire;
        TauxRemise = RegleRemise.Calculer(tauxRemiseClient, quantite);
    }

    public int Id { get; private set; }
    public Guid CommandeId { get; private set; }
    public string ReferenceArticle { get; private set; } = null!;
    public int Quantite { get; private set; }
    public decimal PrixUnitaire { get; private set; }
    public decimal TauxRemise { get; private set; }
    public string? EntrepotAffecte { get; private set; }
    public decimal MontantNet => Math.Round(Quantite * PrixUnitaire * (1 - TauxRemise), 2, MidpointRounding.AwayFromZero);

    internal void AffecterEntrepot(string entrepot) => EntrepotAffecte = entrepot;
}
```

```csharp
// Events/CommandeValidee.cs
namespace Textinord.Trame.Domain.Events;

public sealed record CommandeValidee(
    Guid CommandeId, string Numero, string CodeClient, DateTimeOffset ValideeLe, IReadOnlyList<LigneAPreparer> Lignes);

public sealed record LigneAPreparer(string Reference, int Quantite, string Entrepot);
```

```csharp
// Services/IStockDisponible.cs
namespace Textinord.Trame.Domain.Services;

public interface IStockDisponible
{
    Task<string?> PremierEntrepotDisponibleAsync(string reference, int quantite, CancellationToken ct = default);
}
```

```csharp
// Commande.cs
using Textinord.Trame.Domain.Events;

namespace Textinord.Trame.Domain;

public sealed class Commande
{
    private readonly List<LigneCommande> _lignes = [];

    private Commande()
    {
    }

    public Guid Id { get; private set; }
    public string Numero { get; private set; } = null!;
    public string CodeClient { get; private set; } = null!;
    public DateTimeOffset CreeeLe { get; private set; }
    public DateTimeOffset? ValideeLe { get; private set; }
    public StatutCommande Statut { get; private set; }
    public IReadOnlyList<LigneCommande> Lignes => _lignes;
    public decimal MontantNet => _lignes.Sum(l => l.MontantNet);

    public static Resultat<Commande> Creer(string numero, string codeClient, DateTimeOffset creeeLe, IEnumerable<LigneCommande> lignes)
    {
        var erreurs = new List<string>();
        var liste = lignes.ToList();
        if (!NumeroCommande.EstValide(numero)) erreurs.Add($"Numéro de commande invalide : {numero}.");
        if (string.IsNullOrWhiteSpace(codeClient)) erreurs.Add("Le code client est obligatoire.");
        if (liste.Count == 0) erreurs.Add("Une commande doit contenir au moins une ligne.");
        if (erreurs.Count > 0) return Resultat<Commande>.Echec([.. erreurs]);

        var commande = new Commande { Id = Guid.NewGuid(), Numero = numero, CodeClient = codeClient, CreeeLe = creeeLe, Statut = StatutCommande.Brouillon };
        commande._lignes.AddRange(liste);
        return Resultat<Commande>.Succes(commande);
    }

    /// <summary>Validée si chaque ligne a un entrepôt ; sinon EnAttenteStock. Retourne l'événement à publier, ou null.</summary>
    public CommandeValidee? Valider(IReadOnlyDictionary<string, string?> entrepotParReference, DateTimeOffset validationLe)
    {
        if (Statut is not (StatutCommande.Brouillon or StatutCommande.EnAttenteStock))
            throw new InvalidOperationException($"La commande {Numero} est au statut {Statut} : elle ne peut plus être validée.");

        var enRupture = _lignes.Where(l => !entrepotParReference.TryGetValue(l.ReferenceArticle, out var e) || e is null).ToList();
        if (enRupture.Count > 0)
        {
            Statut = StatutCommande.EnAttenteStock;
            return null;
        }

        foreach (var ligne in _lignes) ligne.AffecterEntrepot(entrepotParReference[ligne.ReferenceArticle]!);
        Statut = StatutCommande.Validee;
        ValideeLe = validationLe;
        return new CommandeValidee(Id, Numero, CodeClient, validationLe,
            _lignes.Select(l => new LigneAPreparer(l.ReferenceArticle, l.Quantite, l.EntrepotAffecte!)).ToList());
    }
}
```

```csharp
// OrdrePreparation.cs
using Textinord.Trame.Domain.Events;

namespace Textinord.Trame.Domain;

public enum StatutOrdre { EnAttente, EnCours, Clos }

public sealed class OrdrePreparation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CommandeId { get; init; }
    public required string NumeroCommande { get; init; }
    public required string Entrepot { get; init; }
    public int NombreLignes { get; init; }
    public int NombrePieces { get; init; }
    public DateTimeOffset EmisLe { get; init; }
    public StatutOrdre Statut { get; private set; } = StatutOrdre.EnAttente;
    public string? Preparateur { get; private set; }

    public static OrdrePreparation Depuis(CommandeValidee commande, IGrouping<string, LigneAPreparer> lignesEntrepot, DateTimeOffset emisLe) => new()
    {
        CommandeId = commande.CommandeId,
        NumeroCommande = commande.Numero,
        Entrepot = lignesEntrepot.Key,
        NombreLignes = lignesEntrepot.Count(),
        NombrePieces = lignesEntrepot.Sum(l => l.Quantite),
        EmisLe = emisLe,
    };
}
```

Point de contrôle 0 : `dotnet build` réussit sur la solution vide de logique (les projets `Infrastructure`, `Api`, `Worker` ne contiennent encore que leur `Program.cs` généré).

## Étapes

### Étape 1 — Persistance et table Outbox (15 min)

Dans `Infrastructure` :

1. `TrameDbContext` avec les `DbSet` `Clients`, `Articles`, `Stocks`, `Commandes`, `OrdresPreparation`, `Compteurs` (un compteur par année pour la numérotation) et `Outbox`. Configurez : `Numero` unique, `Statut` stocké en chaîne, la collection `Lignes` lue par le champ `_lignes` (`HasField` + `UsePropertyAccessMode(PropertyAccessMode.Field)`), un **index unique** sur `(CommandeId, Entrepot)` pour les ordres.
2. `OutboxMessage` : `Id` (long), `MessageId` (Guid, généré), `Type` (nom court), `Contenu` (JSON), `CreeLe`, `EnvoyeLe` (nullable), `Tentatives`, `DerniereErreur`.
3. `OutboxSerialiseur` : `Emballer(object evenement, DateTimeOffset creeLe)` et `Deballer(OutboxMessage)` avec un **registre explicite** des types autorisés (`nameof(CommandeValidee)` → `typeof(CommandeValidee)`). Jamais de `Type.GetType()` sur une chaîne venue de la base.
4. `StockParTable : IStockDisponible` (l'entrepôt le mieux fourni sert la ligne), `DonneesInitiales` (trois clients dont un inactif, quatre articles, stocks par entrepôt, dont une référence à stock nul dans les deux entrepôts), un `IHostedService` `InitialisationBase` qui fait `EnsureCreatedAsync`, active `PRAGMA journal_mode=WAL` et insère les données.
5. Une méthode d'extension `AddTrameInfrastructure(IConfiguration)` qui enregistre tout cela ; chaîne de connexion `ConnectionStrings:Trame`, par défaut `Data Source=../../data/trame2.db` dans les `appsettings.json` de l'API et du Worker (même fichier pour les deux processus).

Point de contrôle 1 : `dotnet run --project src/Textinord.Trame.Api` démarre, le fichier `data/trame2.db` existe et `sqlite3 data/trame2.db .tables` liste `Outbox`, `Commandes`, `OrdresPreparation`.

### Étape 2 — L'API transactionnelle (15 min)

Dans `Api` :

1. Contrats : `NouvelleCommandeRequete(string CodeClient, IReadOnlyList<LigneRequete> Lignes)`, `LigneRequete(string Reference, int Quantite)`, `CommandeReponse` (numéro, client, statut, montant, dates, lignes avec remise et entrepôt).
2. `PriseDeCommande` (service scoped) : contrôle du client et des articles, numérotation `CMD-AAAA-NNNNNN` via le compteur de l'année, création de la commande, interrogation de `IStockDisponible` **par ligne**, `commande.Valider(...)`, puis `db.Commandes.Add(commande)`, `db.Outbox.Add(OutboxSerialiseur.Emballer(evenement, maintenant))` si l'événement existe, et **un seul** `SaveChangesAsync`.
3. Endpoints : `POST /commandes` (201 + `Location`, ou 400 `ValidationProblem`), `GET /commandes/{numero}` (200 ou 404), `GET /commandes/{numero}/ordres-preparation`, `GET /diagnostic/outbox`. `AddProblemDetails()`, `AddOpenApi()`, `MapOpenApi()`.
4. En fin de `Program.cs` : `public partial class Program;` pour les tests d'intégration.

Point de contrôle 2 :

```bash
curl -s -X POST http://localhost:5106/commandes -H 'Content-Type: application/json' \
  -d '{"codeClient":"C-0001","lignes":[{"reference":"VT-1001","quantite":120},{"reference":"LH-3300","quantite":40}]}'
curl -s http://localhost:5106/diagnostic/outbox
```

La première réponse est un 201 avec `"statut":"Validee"` et un entrepôt par ligne ; la seconde montre une ligne `CommandeValidee` avec `envoyeLe: null`. Une commande sur la référence à stock nul répond 201 avec `"statut":"EnAttenteStock"` et **aucune** ligne Outbox.

### Étape 3 — Le Worker : relais Outbox et consumer idempotent (20 min)

Dans `Worker` :

1. `AddTrameMessaging(IConfiguration)` : `AddMassTransit` avec `SetKebabCaseEndpointNameFormatter()`, `AddConsumer<OrdrePreparationConsumer>()`, puis `UsingInMemory` par défaut ou `UsingRabbitMq` si `Messaging:Transport` vaut `RabbitMq` (hôte, vhost, identifiants lus dans `Messaging:RabbitMq`). Dans les deux cas : `UseMessageRetry(r => r.Intervals(1 s, 5 s, 30 s))` puis `ConfigureEndpoints(contexte)`.
2. `OutboxRelay` (scoped) : lit les messages `EnvoyeLe == null && Tentatives < max` par `Id` croissant, par lot ; pour chacun, `Deballer`, `publication.Publish(contenu, type, Pipe.Execute<PublishContext>(c => c.MessageId = message.MessageId), ct)`, puis `EnvoyeLe = horloge.GetUtcNow()` ; en cas d'exception, `Tentatives++` et `DerniereErreur`. Un `SaveChangesAsync` à la fin du lot. Retourne le nombre publié.
3. `OutboxRelayService : BackgroundService` : `PeriodicTimer` sur `Outbox:Intervalle` (2 s), un scope DI par cycle, journalisation, une exception de cycle ne tue pas le Worker.
4. `OrdrePreparationConsumer : IConsumer<CommandeValidee>` : lit les entrepôts déjà servis pour cette commande, crée un `OrdrePreparation` par entrepôt manquant (`OrdrePreparation.Depuis`), `SaveChangesAsync` ; si une `DbUpdateException` de contrainte unique survient (deux instances en parallèle), journalise et sort sans erreur.

Point de contrôle 3 : lancez l'API et le Worker dans deux terminaux, postez une commande. Dans les deux secondes, le Worker journalise « 1 message(s) Outbox publié(s) » puis « Ordre de préparation RBX émis pour CMD-2026-000001 ». `GET /commandes/CMD-2026-000001/ordres-preparation` renvoie les ordres ; `GET /diagnostic/outbox` montre `envoyeLe` renseigné. Test de robustesse : arrêtez le Worker, postez trois commandes, relancez-le : trois ordres, jamais six. Remettez `envoyeLe` à `NULL` sur une ligne (`sqlite3 data/trame2.db "UPDATE Outbox SET EnvoyeLe = NULL WHERE Id = 1"`) : le message est republié, aucun ordre en double.

### Étape 4 — Tests unitaires et d'intégration (20 min)

Au moins dix tests verts, répartis ainsi :

1. `Domain.Tests` (xUnit pur) : remise cumulée et plafonnée (théorie sur cinq cas), commande sans ligne refusée, `Valider` affecte les entrepôts et émet l'événement, `Valider` passe en attente de stock si une ligne n'a pas d'entrepôt.
2. `Worker.Tests` :
   - `OutboxRelayTests` avec `IPublishEndpoint` et `TimeProvider` substitués (NSubstitute) et une SQLite en mémoire (`Data Source=:memory:` sur une connexion gardée ouverte) : publie et marque envoyés ; ne republie pas un message déjà envoyé ; compte les tentatives quand `Publish` lève (`ThrowsAsync`).
   - `OrdrePreparationConsumerTests` avec `ConsumeContext<CommandeValidee>` substitué : un ordre par entrepôt ; trois consommations du même message = toujours deux ordres.
   - `BusEnMemoireTests` avec `AddMassTransitTestHarness` : le relais publie, `harness.Consumed.Any<CommandeValidee>()` est vrai, les ordres sont en base.
3. `Api.Tests` avec `WebApplicationFactory<Program>` et une SQLite temporaire injectée par `builder.UseSetting("ConnectionStrings:Trame", ...)` : `POST` valide → 201 et ligne Outbox dans la même base ; rupture → `EnAttenteStock` sans Outbox ; sans ligne → 400 `application/problem+json` ; `GET` inconnu → 404.

Point de contrôle 4 : `dotnet test` affiche au moins dix réussites, zéro échec, et `dotnet test --collect:"XPlat Code Coverage"` produit un `coverage.cobertura.xml` par projet de tests.

### Étape 5 — Pipelines (10 min)

1. `azure-pipelines.yml` à la racine : déclencheurs `main` et `release/*` ; variables ; cinq stages `Build` (checkout `fetchDepth: 0`, `UseDotNet@2`, restore, build), `Test` (`DotNetCoreCLI@2` avec `--collect:"XPlat Code Coverage" --logger trx`, `PublishCodeCoverageResults@2`), `Publish` (publish de l'API et du Worker, `PublishPipelineArtifact@1`, pas sur une pull request), `Deploy_Recette` (`deployment` job, `environment: trame2-recette`), `Deploy_Production` (`environment: trame2-production`, condition sur `refs/heads/release/`). Commentez chaque stage : ce qu'il fait, pourquoi il existe.
2. `.github/workflows/ci.yml` équivalent : jobs `build-test`, `publish`, `deploy-recette`, `deploy-production` avec `environment:` et conditions `if:` sur les branches.

Point de contrôle 5 : les deux fichiers passent un validateur YAML (extension VS Code « YAML », ou `python3 -c "import yaml,sys; yaml.safe_load(open(sys.argv[1]))" azure-pipelines.yml`) et vous savez expliquer où se configure l'approbation avant production dans chacun des deux outils.

### Étape 6 — Version et notes de release (10 min)

1. `version.json` à la racine avec `"version": "2.1-beta"`, `publicReleaseRefSpec` pour `main` et `release/vX.Y`, section `release` (`branchName`, `versionIncrement: minor`) ; `Nerdbank.GitVersioning` référencé dans `Directory.Build.props` avec `PrivateAssets="all"` (et sa version dans `Directory.Packages.props`).
2. Un endpoint `GET /version` qui renvoie l'`AssemblyInformationalVersion` de l'API.
3. `CHANGELOG.md` au format Keep a Changelog : section « Non publié — 2.1.0-beta » avec Ajouté / Modifié / Technique, une section 2.0.0 datée, les liens de comparaison. Chaque ligne cite un identifiant de work item.

Point de contrôle 6 : `dotnet build` réussit toujours ; `curl http://localhost:5106/version` renvoie `2.1.0-beta` (hors dépôt Git) ou `2.1.N-beta+g…` (dans un dépôt).

### Étape 7 — Nettoyage et livraison (5 min)

`.gitignore` (`bin/`, `obj/`, `data/`, `*.db*`, `TestResults/`), suppression des dossiers `bin/` et `obj/`, relecture du `CHANGELOG` à voix haute comme si Awa Diop le lisait aux entrepôts.

## Livrable

Le dossier `trame2/` complet : solution, sept projets, `Directory.Build.props`, `Directory.Packages.props`, `version.json`, `azure-pipelines.yml`, `.github/workflows/ci.yml`, `CHANGELOG.md`, `.gitignore`. `dotnet build` puis `dotnet test` doivent réussir depuis un poste vierge, sans Docker.

## Dépannage

| Symptôme | Cause probable | Correction |
|---|---|---|
| `error NU1903` sur `SQLitePCLRaw` ou `Microsoft.OpenApi` | audit NuGet : dépendance transitive vulnérable, `TreatWarningsAsErrors` | épingler la version corrigée dans `Directory.Packages.props` et la référencer dans le projet concerné |
| `CS0411` sur `ConfigureEndpoints` | la méthode d'extension est générique sur le type de configurateur | déclarer votre helper `ConfigurerEndpoints<T>(IBusRegistrationContext, IBusFactoryConfigurator<T>) where T : IReceiveEndpointConfigurator` |
| Le Worker publie, mais aucun ordre n'apparaît | consumer non enregistré, ou `ConfigureEndpoints` absent | `AddConsumer<OrdrePreparationConsumer>()` **et** `cfg.ConfigureEndpoints(contexte)` dans le transport |
| `SQLite Error 5: database is locked` entre API et Worker | journal non WAL, ou connexion gardée ouverte | `PRAGMA journal_mode=WAL` à l'initialisation ; un scope DI (donc un DbContext) par cycle de relais |
| Les tests d'intégration voient une base vide | `InitialisationBase` non enregistré, ou chaîne de connexion non surchargée | vérifier `AddHostedService<InitialisationBase>()` et `builder.UseSetting("ConnectionStrings:Trame", ...)` dans la factory |
| Le test du harness attend indéfiniment | consumer absent du harness, ou message publié avant `harness.Start()` | `AddMassTransitTestHarness(x => x.AddConsumer<...>())`, `await harness.Start()` dans `InitializeAsync` |
| `POST` renvoie 500 au lieu de 400 | exception levée dans le domaine pour un cas attendu | renvoyer `Resultat<Commande>.Echec(...)` et `TypedResults.ValidationProblem(...)` |
| Deux ordres pour le même entrepôt après un rejeu | vérification d'existence oubliée, ou index unique absent | relire les entrepôts déjà servis avant d'ajouter, et poser l'index `(CommandeId, Entrepot)` |
| `GET /version` renvoie `1.0.0` | Nerdbank.GitVersioning non référencé, ou `version.json` absent de la racine | vérifier le `PackageReference` dans `Directory.Build.props` et l'emplacement du fichier |

## Grille d'évaluation

| Critère | Points | Ce qui est regardé |
|---|---|---|
| Écriture transactionnelle commande + Outbox | 4 | un seul `SaveChangesAsync`, aucune commande validée sans ligne Outbox, aucune ligne Outbox pour une commande en attente de stock |
| Relais Outbox | 3 | lot ordonné, `MessageId` réutilisé, marquage `EnvoyeLe`, tentatives comptées, le Worker survit à une erreur |
| Consumer idempotent | 3 | un ordre par entrepôt, rejeu sans doublon, index unique en base, transport RabbitMQ activable par configuration |
| Tests | 4 | au moins dix tests verts, substituts NSubstitute utilisés à bon escient, `WebApplicationFactory` sur SQLite, test harness MassTransit |
| Pipelines | 3 | stages complets et commentés, résultats de tests et couverture publiés, approbation avant production, workflow GitHub équivalent |
| Version et release | 2 | `version.json` cohérent, `GET /version`, `CHANGELOG` lisible par le métier |
| Qualité du dépôt | 1 | `Directory.Build.props` / `Directory.Packages.props`, `.gitignore`, aucun `bin/` ni `obj/` livré, build sans avertissement |
| **Total** | **20** | |

## Teardown

```bash
# arrêter l'API et le Worker (Ctrl+C dans chaque terminal)
dotnet clean
rm -rf data/ TestResults/
find . -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
```

Si vous avez lancé RabbitMQ dans Docker : `docker rm -f rabbitmq-trame2`.
