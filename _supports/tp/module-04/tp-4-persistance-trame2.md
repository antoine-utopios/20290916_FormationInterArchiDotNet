# TP 4 — Persistance de Trame 2 : EF Core 10, repository, concurrence, JSON, Dapper

> Module : 4 — Persistance : SQL, NoSQL, ADO.NET et EF Core
> Durée : 75 min (version courte en 50 min : étapes 1 à 6)
> Difficulté : 3 / 5
> Type : TP guidé, en binôme, code compilé et testé

## Mise en situation

Sofia Marques a validé le noyau métier de Trame 2 au TP 1. Julien Delcourt a tranché l'ADR « stockage » ce matin : les commandes vivent dans Azure SQL, accédées par EF Core 10, et personne ne réécrit d'unité de travail par-dessus le `DbContext`. Reste à construire `Textinord.Trame.Infrastructure` : le mapping du modèle, la migration initiale, un repository pour l'agrégat `Commande`, la protection contre les modifications concurrentes (l'ADV et l'extranet touchent les mêmes commandes), les métadonnées de commande en colonne JSON, et une lecture Dapper pour l'écran « commandes du jour » de Marc Vandewalle.

Contrainte de l'atelier : aucun SQL Server sur les postes. Le provider SQLite est le plan B officiel de Trame 2 pour le développement et les tests d'intégration ; le code doit rester identique pour Azure SQL.

## Objectifs

À la fin de ce TP, vous aurez :

- un projet `Textinord.Trame.Infrastructure` avec un `DbContext` EF Core 10 configuré en fluent, sans attribut dans le domaine ;
- une migration initiale générée par `dotnet ef` et appliquée par les tests ;
- un repository `ICommandeRepository` et le `DbContext` exposé comme unité de travail ;
- un jeton de concurrence optimiste, prouvé par un test qui provoque le conflit ;
- les métadonnées de commande stockées en JSON et filtrables en LINQ ;
- une requête de lecture Dapper sur la même connexion ;
- au moins huit tests d'intégration xUnit sur SQLite en mémoire, tous verts.

## Prérequis

- SDK .NET 10.0.3xx ; `export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1`
- Accès NuGet (une fois, pour restaurer `Microsoft.EntityFrameworkCore.Sqlite` 10.0.x, `Dapper`, `xunit`)
- Le projet `Textinord.Trame.Domain` du TP 1. Si vous ne l'avez pas, l'annexe A fournit les classes nécessaires, complètes.
- Avoir suivi la démo 4.1 (le SQL généré, le conflit de version)

## Point de départ

Dans un dossier vide :

```bash
mkdir trame2-persistance && cd trame2-persistance
dotnet new sln -n Textinord.Trame --format sln
dotnet new classlib -n Textinord.Trame.Domain -o src/Textinord.Trame.Domain
dotnet new classlib -n Textinord.Trame.Infrastructure -o src/Textinord.Trame.Infrastructure
dotnet new xunit -n Textinord.Trame.Infrastructure.Tests -o tests/Textinord.Trame.Infrastructure.Tests
dotnet sln add src/Textinord.Trame.Domain src/Textinord.Trame.Infrastructure tests/Textinord.Trame.Infrastructure.Tests
rm src/Textinord.Trame.Domain/Class1.cs src/Textinord.Trame.Infrastructure/Class1.cs tests/Textinord.Trame.Infrastructure.Tests/UnitTest1.cs
dotnet new tool-manifest
dotnet tool install dotnet-ef
```

Créez `Directory.Build.props` à la racine :

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
</Project>
```

et `Directory.Packages.props` :

```xml
<Project>
  <ItemGroup>
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.11" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.11" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.11" />
    <PackageVersion Include="Dapper" Version="2.1.79" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.11" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.9.0" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
  </ItemGroup>
</Project>
```

Retirez les attributs `Version="..."` des `PackageReference` que `dotnet new xunit` a générés dans le projet de tests (le Central Package Management les interdit), puis copiez dans `src/Textinord.Trame.Domain` vos classes du TP 1 ou celles de l'annexe A.

Ajoutez les références :

```bash
cd src/Textinord.Trame.Infrastructure
dotnet add reference ../Textinord.Trame.Domain
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Dapper
dotnet add package Microsoft.Extensions.DependencyInjection.Abstractions
cd ../../tests/Textinord.Trame.Infrastructure.Tests
dotnet add reference ../../src/Textinord.Trame.Domain ../../src/Textinord.Trame.Infrastructure
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
cd ../..
```

## Étapes

### Étape 1 — Compiler le squelette (5 min)

Point de contrôle 1 : `dotnet build` se termine sans erreur, les trois projets apparaissent dans `dotnet sln list`, `dotnet dotnet-ef --version` affiche 10.0.x.

### Étape 2 — Le `DbContext` et les configurations fluent (15 min)

Dans `src/Textinord.Trame.Infrastructure/Persistence`, créez `TrameDbContext` (constructeur primaire prenant `DbContextOptions<TrameDbContext>`, quatre `DbSet`, `OnModelCreating` qui appelle `ApplyConfigurationsFromAssembly`) et une classe `IEntityTypeConfiguration<T>` par entité dans `Persistence/Configurations`.

Exigences de mapping :

| Entité | À configurer |
|---|---|
| `Client` | table `Clients` ; `Code` (20, requis, index unique) ; `RaisonSociale` (200, requis) ; `ConditionTarifaire` stockée en texte (`HasConversion<string>()`) |
| `Article` | table `Articles` ; `Reference` (30, unique) ; `Famille` indexée ; `PrixBase` en précision (18, 2) ; `Stocks` en collection possédée (`OwnsMany`) dans une table `StocksArticle` avec un `Id` ombre et un index unique (`ArticleId`, `CodeEntrepot`) ; navigation par le champ `_stocks` |
| `Commande` | table `Commandes` ; `Numero` (15, unique) ; `Statut` en texte, indexé ; `Date` indexée ; relation vers `Client` sans suppression en cascade ; `Lignes` : relation un-à-plusieurs avec clé étrangère ombre `CommandeId` requise, cascade, navigation par le champ `_lignes` ; `TotalHt` ignoré |
| `LigneCommande` | table `LignesCommande` ; `ReferenceArticle` (30) ; `PrixUnitaire` (18, 2) ; `RemisePourcent` (5, 2) ; relation vers `Article` sans navigation, sans cascade ; `MontantHt` ignoré |

Ajoutez une fabrique de conception `IDesignTimeDbContextFactory<TrameDbContext>` qui construit le contexte sur `UseSqlite("Data Source=trame2.db")`, avec la configuration SQL Server en commentaire.

Point de contrôle 2 : `dotnet build` vert et `dotnet dotnet-ef dbcontext info --project src/Textinord.Trame.Infrastructure` affiche `Provider name: Microsoft.EntityFrameworkCore.Sqlite`.

### Étape 3 — La migration initiale (5 min)

```bash
dotnet dotnet-ef migrations add Initiale --project src/Textinord.Trame.Infrastructure --output-dir Persistence/Migrations
```

Ouvrez le fichier généré et vérifiez, avant d'aller plus loin : cinq tables (`Clients`, `Articles`, `StocksArticle`, `Commandes`, `LignesCommande`), la clé étrangère `CommandeId` non nullable avec `onDelete: Cascade`, `ClientId` avec `Restrict`, les index uniques sur `Code`, `Reference`, `Numero`. Si un point manque, `dotnet dotnet-ef migrations remove` puis corrigez la configuration : une migration se régénère, elle ne se retouche pas à la main.

Point de contrôle 3 : la migration contient exactement les cinq tables et les index attendus.

### Étape 4 — Repository et unité de travail (10 min)

Dans le domaine, déclarez les contrats (ils appartiennent au domaine, pas à l'infrastructure) :

```csharp
public interface ICommandeRepository
{
    Task<Commande?> ObtenirAsync(int id, CancellationToken ct = default);
    Task<Commande?> ObtenirParNumeroAsync(string numero, CancellationToken ct = default);
    Task<string> ProchainNumeroAsync(int annee, CancellationToken ct = default);
    Task<IReadOnlyList<Commande>> ListerParStatutAsync(StatutCommande statut, CancellationToken ct = default);
    void Ajouter(Commande commande);
}

public interface IUniteDeTravail
{
    Task<int> SauvegarderAsync(CancellationToken ct = default);
}
```

Implémentez `CommandeRepository` dans Infrastructure : `ObtenirAsync` et `ObtenirParNumeroAsync` chargent l'agrégat complet (client et lignes) ; `ListerParStatutAsync` lit sans suivi ; `ProchainNumeroAsync` calcule `CMD-AAAA-NNNNNN` à partir du dernier numéro de l'année ; `Ajouter` ne sauvegarde pas. Faites implémenter `IUniteDeTravail` par `TrameDbContext`.

Ajoutez une méthode d'extension `AddTramePersistence(this IServiceCollection, string chaine)` qui enregistre le contexte (SQLite ; SQL Server en commentaire), le repository, l'unité de travail.

Point de contrôle 4 : un premier test d'intégration passe : ajouter une commande de deux lignes, la relire dans un second contexte, retrouver le client, les deux lignes et le bon numéro.

### Étape 5 — Concurrence optimiste (10 min)

Ajoutez sur `Commande` une propriété `Version` (`Guid`, setter privé), configurée `IsConcurrencyToken()`. Dans `TrameDbContext`, surchargez `SaveChangesAsync` pour renouveler la version de toute commande modifiée, directement ou par ses lignes ou ses métadonnées. Régénérez la migration si vous aviez déjà généré la vôtre sans cette colonne (`migrations remove`, puis `migrations add Initiale`).

Écrivez le test du conflit : deux contextes sur la même connexion chargent la même commande ; le premier la valide et sauvegarde ; le second l'annule et sauvegarde : `DbUpdateConcurrencyException` attendue. Terminez le test par un `ReloadAsync()` et vérifiez que la commande relue est au statut écrit par le premier.

Point de contrôle 5 : le test de conflit est vert, et un test montre que la version change après une modification.

### Étape 6 — Métadonnées en colonne JSON (5 min)

Mappez `Commande.Metadonnees` (`MetadonneesCommande` : origine, référence client, commentaire, étiquettes) en owned type sérialisé dans une colonne JSON : `OwnsOne(c => c.Metadonnees, m => m.ToJson("Metadonnees"))`. Régénérez la migration si nécessaire.

Tests attendus : la colonne contient bien du JSON (lisez-la en SQL brut avec `SqliteCommand` et vérifiez la présence de `"Origine":"EDI"`), la relecture restitue les étiquettes, et un `Where(c => c.Metadonnees.Origine == "Extranet")` filtre correctement.

Point de contrôle 6 : `dotnet test` affiche au moins cinq tests verts.

### Étape 7 — Lecture Dapper (10 min)

Créez dans Infrastructure un dossier `Lecture` avec un record `CommandeResume(string Numero, string Client, string Statut, int NombreLignes, decimal TotalHt)`, une interface `ICommandeLecture` (`ResumesDuJourAsync(DateOnly jour)`) et son implémentation Dapper qui reçoit un `DbConnection`. SQL attendu : jointure `Commandes` / `Clients`, jointure externe sur `LignesCommande`, `COUNT`, somme de `Quantite * PrixUnitaire * (1 - RemisePourcent / 100.0)`, filtre sur la journée, `GROUP BY`, tri par numéro. Enregistrez `DbConnection` dans `AddTramePersistence` à partir de `Database.GetDbConnection()` du contexte (même connexion, même durée de vie).

Attention aux types : SQLite renvoie `COUNT` en entier 64 bits et une somme sur des décimaux en réel ; mappez d'abord vers un record intermédiaire puis convertissez.

Point de contrôle 7 : un test insère une commande du jour et une commande de la veille, puis vérifie qu'une seule ligne revient, avec le bon nombre de lignes et le bon montant.

### Étape 8 — Compléter la batterie de tests (10 min)

Atteignez au moins huit tests, dont : la migration s'applique et laisse aucune migration en attente ; la numérotation est séquentielle par année ; `ListerParStatutAsync` ne laisse aucune entité suivie ; une commande sans stock passe `EnAttenteStock` ; annuler une commande en préparation lève `RegleMetierException` ; `ExecuteUpdateAsync` facture en masse les commandes expédiées.

Socle recommandé pour les tests : une classe de base qui ouvre une `SqliteConnection("Data Source=:memory:")`, applique `Database.Migrate()` et fournit une méthode `CreerContexte()` retournant un nouveau `TrameDbContext` sur cette connexion.

Point de contrôle 8 : `dotnet test` : au moins 8 tests, 0 échec.

## Livrable

Le dossier de la solution, sans `bin/` ni `obj/`, avec un `.gitignore`, contenant :

- `src/Textinord.Trame.Domain` (inchangé ou complété de `Version`, `ICommandeRepository`, `IUniteDeTravail`) ;
- `src/Textinord.Trame.Infrastructure` : `Persistence/TrameDbContext.cs`, `Persistence/Configurations/*.cs`, `Persistence/Migrations/*`, `Persistence/Repositories/CommandeRepository.cs`, `Persistence/TrameDbContextFactory.cs`, `Lecture/*.cs`, `DependencyInjection.cs` ;
- `tests/Textinord.Trame.Infrastructure.Tests` : au moins huit tests verts ;
- `dotnet-tools.json` (ou `.config/dotnet-tools.json`) avec `dotnet-ef`.

## Dépannage

| Symptôme | Cause probable | Correction |
|---|---|---|
| `Property 'LigneCommande.CommandeId' is of type 'int?'` | clé étrangère ombre déclarée sans `IsRequired()` | ajouter `.IsRequired()` après `HasForeignKey("CommandeId")`, régénérer la migration |
| `No suitable constructor was found for entity type` | pas de constructeur sans paramètre accessible à EF | ajouter un constructeur privé sans paramètre qui initialise les chaînes |
| `The navigation 'Commande.Lignes' ... no backing field` | nom de champ différent de `_lignes` | `HasField("_lignes")` doit correspondre au nom exact du champ |
| `SQLite Error 1: 'no such table'` dans un test | la connexion s'est fermée entre deux contextes | garder la `SqliteConnection` ouverte pendant tout le test ; une base `:memory:` disparaît avec sa dernière connexion |
| `Unable to create a 'DbContext' of type ...` avec `dotnet ef` | pas de fabrique de conception | ajouter `IDesignTimeDbContextFactory<TrameDbContext>` dans Infrastructure |
| Dapper : `A parameterless default constructor or one matching signature (... Int64 ...)` | `COUNT` SQLite → `long` | record intermédiaire avec `long NombreLignes` et `double TotalHt` |
| `DbUpdateConcurrencyException` jamais levée | les deux contextes n'ont pas lu la même version, ou la version n'est pas renouvelée | charger les deux commandes avant la première sauvegarde ; vérifier la surcharge de `SaveChangesAsync` |
| `Assert.Single()` échoue sur `exception.Entries` (2 éléments) | l'owned type JSON est aussi dans l'exception | chercher l'entrée dont l'entité est une `Commande` |
| `NU1008` sur le projet de tests | `Version` laissé sur un `PackageReference` | retirer les versions des `PackageReference`, elles sont dans `Directory.Packages.props` |

## Grille d'évaluation

| Critère | Points | Observable |
|---|---|---|
| Solution compilable, structure `src/` / `tests/`, CPM, pas de version dans les csproj | 2 | `dotnet build` vert |
| Mapping fluent complet, domaine sans attribut EF, champs privés respectés | 3 | configurations, `Navigation(...).HasField(...)` |
| Migration initiale générée par l'outil, tables et index conformes | 2 | fichier de migration, `migrations list` |
| Repository par agrégat + `DbContext` comme unité de travail, `AddTramePersistence` | 3 | `CommandeRepository`, `IUniteDeTravail`, `DependencyInjection.cs` |
| Concurrence optimiste implémentée et testée (conflit provoqué, `ReloadAsync`) | 3 | test `DbUpdateConcurrencyException` vert |
| Colonne JSON : écriture, relecture, filtre LINQ | 2 | tests JSON verts |
| Lecture Dapper correcte (agrégats, types SQLite) | 2 | test Dapper vert |
| Au moins 8 tests d'intégration verts sur SQLite en mémoire | 3 | `dotnet test` |
| **Total** | **20** | |

Bonus (hors barème) : `AsSplitQuery()` justifié sur `ObtenirAsync`, requête compilée pour `ObtenirParNumeroAsync`, script idempotent généré (`migrations script --idempotent`).

## Teardown

```bash
dotnet clean
find . -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
rm -f src/Textinord.Trame.Infrastructure/trame2.db
```

Conservez le dossier : le TP 5 (API) et le TP 6 (Outbox, tests d'intégration avec `WebApplicationFactory`) repartent de cette persistance.

## Annexe A — Domaine de départ (si vous n'avez pas le TP 1)

Copiez ces fichiers dans `src/Textinord.Trame.Domain`. Ils ne dépendent d'aucun paquet.

`RegleMetierException.cs`

```csharp
namespace Textinord.Trame.Domain;

public sealed class RegleMetierException(string message) : Exception(message);
```

`StatutCommande.cs`

```csharp
namespace Textinord.Trame.Domain;

public enum StatutCommande
{
    Brouillon,
    EnAttenteStock,
    Validee,
    EnPreparation,
    Expediee,
    Facturee,
    Annulee
}
```

`ConditionTarifaire.cs`

```csharp
namespace Textinord.Trame.Domain;

public enum ConditionTarifaire
{
    Standard,
    Collectivite,
    GrandCompte
}

public static class ConditionTarifaireExtensions
{
    public static decimal RemisePourcent(this ConditionTarifaire condition) => condition switch
    {
        ConditionTarifaire.Standard => 0m,
        ConditionTarifaire.Collectivite => 12m,
        ConditionTarifaire.GrandCompte => 25m,
        _ => throw new ArgumentOutOfRangeException(nameof(condition))
    };
}
```

`NumeroCommande.cs`

```csharp
using System.Text.RegularExpressions;

namespace Textinord.Trame.Domain;

public static partial class NumeroCommande
{
    public const string Prefixe = "CMD-";

    [GeneratedRegex(@"^CMD-\d{4}-\d{6}$")]
    private static partial Regex Format();

    public static bool EstValide(string numero) => !string.IsNullOrEmpty(numero) && Format().IsMatch(numero);

    public static string Former(int annee, int sequence) => $"{Prefixe}{annee:D4}-{sequence:D6}";

    public static int Sequence(string numero) =>
        EstValide(numero) ? int.Parse(numero[^6..]) : throw new RegleMetierException($"Numéro invalide : {numero}");
}
```

`Client.cs`

```csharp
namespace Textinord.Trame.Domain;

public sealed class Client
{
    public int Id { get; private set; }
    public string Code { get; private set; }
    public string RaisonSociale { get; private set; }
    public ConditionTarifaire ConditionTarifaire { get; private set; }

    private Client()
    {
        Code = string.Empty;
        RaisonSociale = string.Empty;
    }

    public Client(string code, string raisonSociale, ConditionTarifaire conditionTarifaire)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(raisonSociale);
        Code = code.Trim().ToUpperInvariant();
        RaisonSociale = raisonSociale.Trim();
        ConditionTarifaire = conditionTarifaire;
    }

    public decimal RemisePourcent => ConditionTarifaire.RemisePourcent();

    public void ChangerConditionTarifaire(ConditionTarifaire nouvelleCondition) =>
        ConditionTarifaire = nouvelleCondition;
}
```

`StockEntrepot.cs`

```csharp
namespace Textinord.Trame.Domain;

public sealed class StockEntrepot
{
    public string CodeEntrepot { get; private set; }
    public int Quantite { get; private set; }

    private StockEntrepot()
    {
        CodeEntrepot = string.Empty;
    }

    public StockEntrepot(string codeEntrepot, int quantite)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeEntrepot);
        ArgumentOutOfRangeException.ThrowIfNegative(quantite);
        CodeEntrepot = codeEntrepot.Trim().ToUpperInvariant();
        Quantite = quantite;
    }

    internal void Ajuster(int nouvelleQuantite)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(nouvelleQuantite);
        Quantite = nouvelleQuantite;
    }
}
```

`Article.cs`

```csharp
namespace Textinord.Trame.Domain;

public sealed class Article
{
    private readonly List<StockEntrepot> _stocks = [];

    public int Id { get; private set; }
    public string Reference { get; private set; }
    public string Libelle { get; private set; }
    public string Famille { get; private set; }
    public decimal PrixBase { get; private set; }
    public IReadOnlyCollection<StockEntrepot> Stocks => _stocks.AsReadOnly();

    private Article()
    {
        Reference = string.Empty;
        Libelle = string.Empty;
        Famille = string.Empty;
    }

    public Article(string reference, string libelle, string famille, decimal prixBase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(libelle);
        ArgumentException.ThrowIfNullOrWhiteSpace(famille);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(prixBase);
        Reference = reference.Trim().ToUpperInvariant();
        Libelle = libelle.Trim();
        Famille = famille.Trim();
        PrixBase = prixBase;
    }

    public void DefinirStock(string codeEntrepot, int quantite)
    {
        var code = codeEntrepot.Trim().ToUpperInvariant();
        var existant = _stocks.FirstOrDefault(s => s.CodeEntrepot == code);
        if (existant is null)
        {
            _stocks.Add(new StockEntrepot(code, quantite));
        }
        else
        {
            existant.Ajuster(quantite);
        }
    }

    public bool EstDisponible(int quantite) => _stocks.Any(s => s.Quantite >= quantite);

    public int StockTotal => _stocks.Sum(s => s.Quantite);
}
```

`LigneCommande.cs`

```csharp
namespace Textinord.Trame.Domain;

public sealed class LigneCommande
{
    public int Id { get; private set; }
    public int ArticleId { get; private set; }
    public string ReferenceArticle { get; private set; }
    public int Quantite { get; private set; }
    public decimal PrixUnitaire { get; private set; }
    public decimal RemisePourcent { get; private set; }

    private LigneCommande()
    {
        ReferenceArticle = string.Empty;
    }

    internal LigneCommande(Article article, int quantite, decimal prixUnitaire, decimal remisePourcent)
    {
        ArgumentNullException.ThrowIfNull(article);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantite);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(prixUnitaire);
        ArgumentOutOfRangeException.ThrowIfNegative(remisePourcent);
        ArticleId = article.Id;
        ReferenceArticle = article.Reference;
        Quantite = quantite;
        PrixUnitaire = prixUnitaire;
        RemisePourcent = remisePourcent;
    }

    public decimal MontantHt => Math.Round(Quantite * PrixUnitaire * (1 - RemisePourcent / 100m), 2);
}
```

`MetadonneesCommande.cs`

```csharp
namespace Textinord.Trame.Domain;

public sealed class MetadonneesCommande
{
    public string Origine { get; private set; }
    public string? ReferenceClient { get; private set; }
    public string? Commentaire { get; private set; }
    public List<string> Etiquettes { get; private set; }

    private MetadonneesCommande()
    {
        Origine = "ADV";
        Etiquettes = [];
    }

    public MetadonneesCommande(string origine, string? referenceClient = null, string? commentaire = null,
        IEnumerable<string>? etiquettes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(origine);
        Origine = origine;
        ReferenceClient = referenceClient;
        Commentaire = commentaire;
        Etiquettes = etiquettes?.ToList() ?? [];
    }

    public void Commenter(string commentaire) => Commentaire = commentaire;

    public void Etiqueter(string etiquette)
    {
        if (!Etiquettes.Contains(etiquette, StringComparer.OrdinalIgnoreCase))
        {
            Etiquettes.Add(etiquette);
        }
    }
}
```

`Commande.cs`

```csharp
namespace Textinord.Trame.Domain;

public sealed class Commande
{
    public const decimal PlafondRemisePourcent = 30m;
    public const decimal RemiseVolumePourcent = 5m;
    public const int SeuilRemiseVolume = 500;

    private readonly List<LigneCommande> _lignes = [];

    public int Id { get; private set; }
    public string Numero { get; private set; }
    public int ClientId { get; private set; }
    public Client? Client { get; private set; }
    public DateTime Date { get; private set; }
    public StatutCommande Statut { get; private set; }
    public MetadonneesCommande Metadonnees { get; private set; }
    public Guid Version { get; private set; }
    public IReadOnlyCollection<LigneCommande> Lignes => _lignes.AsReadOnly();

    private Commande()
    {
        Numero = string.Empty;
        Metadonnees = new MetadonneesCommande("ADV");
    }

    public Commande(string numero, Client client, DateTime date, MetadonneesCommande? metadonnees = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (!NumeroCommande.EstValide(numero))
        {
            throw new RegleMetierException($"Numéro de commande invalide : '{numero}' (attendu CMD-AAAA-NNNNNN).");
        }

        Numero = numero;
        Client = client;
        ClientId = client.Id;
        Date = date;
        Statut = StatutCommande.Brouillon;
        Metadonnees = metadonnees ?? new MetadonneesCommande("ADV");
        Version = Guid.NewGuid();
    }

    public decimal TotalHt => _lignes.Sum(l => l.MontantHt);

    public LigneCommande AjouterLigne(Article article, int quantite, decimal? prixNegocie = null)
    {
        ArgumentNullException.ThrowIfNull(article);
        ExigerStatut(StatutCommande.Brouillon, "ajouter une ligne");

        var remiseClient = Client?.RemisePourcent ?? 0m;
        var remiseVolume = quantite > SeuilRemiseVolume ? RemiseVolumePourcent : 0m;
        var remise = Math.Min(remiseClient + remiseVolume, PlafondRemisePourcent);

        var ligne = new LigneCommande(article, quantite, prixNegocie ?? article.PrixBase, remise);
        _lignes.Add(ligne);
        return ligne;
    }

    public void Valider(Func<LigneCommande, bool> stockDisponible)
    {
        ArgumentNullException.ThrowIfNull(stockDisponible);
        if (Statut is not (StatutCommande.Brouillon or StatutCommande.EnAttenteStock))
        {
            throw new RegleMetierException($"Impossible de valider une commande au statut {Statut}.");
        }

        if (_lignes.Count == 0)
        {
            throw new RegleMetierException("Une commande sans ligne ne peut pas être validée.");
        }

        Statut = _lignes.All(stockDisponible) ? StatutCommande.Validee : StatutCommande.EnAttenteStock;
    }

    public void DemarrerPreparation()
    {
        ExigerStatut(StatutCommande.Validee, "démarrer la préparation");
        Statut = StatutCommande.EnPreparation;
    }

    public void Expedier()
    {
        ExigerStatut(StatutCommande.EnPreparation, "expédier");
        Statut = StatutCommande.Expediee;
    }

    public void Facturer()
    {
        ExigerStatut(StatutCommande.Expediee, "facturer");
        Statut = StatutCommande.Facturee;
    }

    public void Annuler(string motif)
    {
        if (Statut is StatutCommande.EnPreparation or StatutCommande.Expediee or StatutCommande.Facturee)
        {
            throw new RegleMetierException($"Une commande {Statut} ne peut plus être annulée.");
        }

        Statut = StatutCommande.Annulee;
        Metadonnees.Commenter($"Annulée : {motif}");
    }

    private void ExigerStatut(StatutCommande attendu, string action)
    {
        if (Statut != attendu)
        {
            throw new RegleMetierException($"Impossible de {action} : statut {Statut}, attendu {attendu}.");
        }
    }
}
```
