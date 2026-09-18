# Solution — TP 4 : Persistance de Trame 2

> Document formateur — Ne pas distribuer avant la fin du TP.
> Code complet et compilable : `solutions/module-04/tp-4/` (`dotnet build`, puis `dotnet test` : 14 tests verts sur SQLite en mémoire, SDK .NET 10.0.302, EF Core 10.0.11).

## Approche pédagogique

Le TP a un fil unique : le domaine du TP 1 ne bouge pas (à trois ajouts près : `Version`, `ICommandeRepository`, `IUniteDeTravail`), et tout ce qui concerne la base vit dans `Textinord.Trame.Infrastructure`. Les binômes qui réussissent sont ceux qui régénèrent la migration sans hésiter à chaque correction de configuration (étapes 2, 5, 6) au lieu de la retoucher à la main. Le test de concurrence est le moment de bascule : quand il passe, le reste (JSON, Dapper) se déroule vite. Prévoyez de projeter la solution des étapes 4 et 5 si plus de la moitié de la salle est en retard à mi-parcours.

Version courte (50 min) : étapes 1 à 6 ; les étapes 7 et 8 sont données comme travail de fin de journée avec la solution en lecture.

## Structure livrée

```text
solutions/module-04/tp-4/
├── Textinord.Trame.sln
├── Directory.Build.props              # net10.0, nullable, CPM, InvariantGlobalization
├── Directory.Packages.props           # EF Core 10.0.11, Dapper 2.1.79, xunit 2.9.3, Test SDK 18.9.0
├── dotnet-tools.json                  # dotnet-ef 10.0.11 (outil local)
├── .gitignore
├── src/
│   ├── Textinord.Trame.Domain/        # entités, statuts, règles, contrats ICommandeRepository / IUniteDeTravail
│   └── Textinord.Trame.Infrastructure/
│       ├── DependencyInjection.cs     # AddTramePersistence (SQLite ; SQL Server commenté)
│       ├── Lecture/                   # CommandeResume, ICommandeLecture, CommandeLectureDapper
│       └── Persistence/
│           ├── TrameDbContext.cs      # DbContext + IUniteDeTravail + renouvellement du jeton
│           ├── TrameDbContextFactory.cs
│           ├── Configurations/        # une IEntityTypeConfiguration<T> par entité
│           ├── Migrations/            # 20260907190616_Initiale + snapshot (générés par dotnet ef)
│           └── Repositories/CommandeRepository.cs
└── tests/Textinord.Trame.Infrastructure.Tests/
    ├── BaseSqliteEnMemoire.cs         # connexion :memory: ouverte, Migrate(), CreerContexte()
    ├── MigrationTests.cs              # 1 test
    ├── CommandeRepositoryTests.cs     # 7 tests
    ├── ConcurrenceOptimisteTests.cs   # 3 tests, dont le conflit
    ├── MetadonneesJsonTests.cs        # 2 tests
    └── CommandeLectureDapperTests.cs  # 1 test
```

Commandes de vérification :

```bash
cd solutions/module-04/tp-4
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
dotnet tool restore
dotnet build
dotnet test
dotnet dotnet-ef migrations list --project src/Textinord.Trame.Infrastructure
```

## Choix d'architecture et justification

### 1. Le domaine ne connaît pas EF Core

Aucun attribut, aucun `using Microsoft.EntityFrameworkCore` dans `Textinord.Trame.Domain`. EF Core matérialise les entités par leur constructeur privé sans paramètre et écrit les propriétés à setter privé ; les collections sont exposées en `IReadOnlyCollection<T>` et remplies par le champ privé (`HasField("_lignes")`, `HasField("_stocks")`). Le prix : trois lignes de configuration par collection. Le gain : `Commande.AjouterLigne` reste le seul chemin pour créer une ligne, en base comme en mémoire.

Les interfaces `ICommandeRepository` et `IUniteDeTravail` sont déclarées dans le domaine : c'est le domaine qui dit ce dont il a besoin, l'infrastructure qui le fournit (inversion de dépendance, module 1).

### 2. Configurations fluent, une classe par entité

`ApplyConfigurationsFromAssembly` découvre les `IEntityTypeConfiguration<T>`. Points qui comptent :

- `HasConversion<string>()` sur `Statut` et `ConditionTarifaire` : la base reste lisible (`'Validee'` plutôt que `2`), Dapper lit directement une chaîne, et l'ajout d'un statut ne renumérote rien.
- `HasForeignKey("CommandeId").IsRequired()` : sans `IsRequired()`, EF crée une clé étrangère nullable (`int?`) et le renouvellement du jeton, qui lit cette propriété ombre en `int`, échoue. C'est l'erreur la plus fréquente du TP (voir le dépannage de l'énoncé).
- `OnDelete(DeleteBehavior.Restrict)` sur `Client` et `Article` : on ne supprime pas un client qui a des commandes ; `Cascade` sur les lignes : elles n'existent pas sans leur commande.
- `OwnsMany` pour les stocks : la collection appartient à l'article, table dédiée `StocksArticle`, index unique (`ArticleId`, `CodeEntrepot`).

### 3. La migration est générée, jamais écrite

`dotnet dotnet-ef migrations add Initiale` avec la fabrique `TrameDbContextFactory` (aucun projet de démarrage nécessaire). La migration livrée contient cinq tables, dix index (dont `IX_Commandes_Numero`, `IX_Clients_Code`, `IX_Articles_Reference` uniques) et les deux comportements de suppression. Le test `MigrationTests` applique la migration sur la base en mémoire et vérifie qu'aucune migration n'est en attente : le snapshot et le modèle sont donc synchronisés, ce qui détecte une configuration modifiée sans migration.

### 4. Repository par agrégat, `DbContext` comme unité de travail

`CommandeRepository` a cinq méthodes et ne sauvegarde jamais : `Ajouter` fait un `Add`, et c'est `IUniteDeTravail.SauvegarderAsync` (implémentée par `TrameDbContext`) qui écrit. Un cas d'usage qui touche une commande et un ordre de préparation appelle deux repositories et une seule sauvegarde : une transaction.

`ListerParStatutAsync` est en `AsNoTracking()` ; le test `Lister_par_statut_ne_suit_pas_les_entites` vérifie `ChangeTracker.Entries()` vide. `ObtenirAsync` charge client et lignes en une requête (jointure) : avec deux collections on passerait à `AsSplitQuery()`.

`ProchainNumeroAsync` lit le dernier numéro de l'année et incrémente. C'est volontairement simple et c'est expliqué comme tel : deux commandes créées au même instant peuvent obtenir le même numéro, et l'index unique sur `Numero` fera échouer la seconde (`DbUpdateException`). En production sur Azure SQL, on remplace par une `SEQUENCE` par année (`CREATE SEQUENCE SeqCommande2026 START WITH 1`, `NEXT VALUE FOR`) ou par une table de compteurs verrouillée dans la transaction (`UPDLOCK`). Une bonne question de débriefing : « qui a vu le problème sans qu'on le dise ? »

### 5. Concurrence optimiste gérée par l'application

`Commande.Version` (`Guid`) est configurée `IsConcurrencyToken()`. `TrameDbContext.RenouvelerLesJetonsDeVersion()` s'exécute avant chaque sauvegarde : il renouvelle la version de toute commande modifiée, ou dont une ligne a été modifiée ou supprimée, ou dont les métadonnées JSON ont changé. EF Core ajoute alors `WHERE Version = @ancienne` à l'UPDATE ; zéro ligne touchée lève `DbUpdateConcurrencyException`.

Pourquoi pas `rowversion` : `IsRowVersion()` n'est implémenté que par SQL Server ; SQLite n'a pas d'équivalent, et le TP impose SQLite comme plan B. Le jeton applicatif fonctionne sur SQLite, SQL Server et PostgreSQL avec le même code. Sur une cible SQL Server pure, on remplacerait par `builder.Property<byte[]>("RowVersion").IsRowVersion()` (ligne laissée en commentaire dans `CommandeConfiguration`).

Le test `Deux_modifications_concurrentes_...` reproduit le scénario de la slide 25 : deux contextes sur la même connexion, Sofia valide, l'extranet annule, exception, `ReloadAsync()`, la commande relue est `Validee`. Détail qui surprend : `exception.Entries` contient deux entrées, la commande et son owned type JSON (l'annulation écrit aussi un commentaire). D'où `Assert.Single(exception.Entries, e => e.Entity is Commande)` plutôt que `Assert.Single(exception.Entries)`.

### 6. Métadonnées en colonne JSON

`OwnsOne(c => c.Metadonnees, m => m.ToJson("Metadonnees"))`. Sur SQLite la colonne est `TEXT`, sur SQL Server `nvarchar(max)` (ou `json` avec SQL Server 2025 / Azure SQL selon la version du provider). `MetadonneesCommande` est une classe mutable à setters privés, modifiée par ses méthodes (`Commenter`, `Etiqueter`) : EF détecte les changements sur l'instance suivie. Une variante avec un `record` immuable et `with` est possible, mais le remplacement d'instance d'un owned type demande plus de précautions (identité de l'owned) ; on la réserve aux complex types (`ComplexProperty(...).ToJson()`, EF Core 10), mentionnés en cours.

Les tests vérifient trois choses : le contenu brut de la colonne (`"Origine":"EDI"`, tableau d'étiquettes), la relecture fidèle, et la traduction d'un `Where` sur `Metadonnees.Origine` (`json_extract` / `->>` sur SQLite, `JSON_VALUE` sur SQL Server).

### 7. Lecture Dapper sur la même connexion

`CommandeLectureDapper` reçoit un `DbConnection` : dans `AddTramePersistence`, c'est `TrameDbContext.Database.GetDbConnection()`, enregistré en scoped. Même connexion, même transaction si une transaction EF est ouverte (`UseTransaction` sinon). Le SQL est portable (aucune fonction propriétaire) ; les seules subtilités sont les types renvoyés par SQLite : `COUNT` en `long`, la somme des décimaux (stockés en texte) en `double`. D'où le record intermédiaire `CommandeResumeBrut(…, long NombreLignes, double TotalHt)` converti en `CommandeResume`. Sur SQL Server, `COUNT` renvoie `int` et `SUM` un `decimal` : le record intermédiaire fonctionne aussi (Dapper convertit les numériques).

### 8. Tests d'intégration sur SQLite en mémoire

`BaseSqliteEnMemoire` ouvre une `SqliteConnection("Data Source=:memory:")` par test et la garde ouverte : une base en mémoire disparaît à la fermeture de sa dernière connexion. `Database.Migrate()` applique la vraie migration (pas `EnsureCreated()`, qui contournerait les migrations). `CreerContexte()` fournit un nouveau `DbContext` sur la même connexion : c'est ce qui permet de simuler deux utilisateurs.

Les 14 tests livrés :

| Fichier | Test | Ce qu'il prouve |
|---|---|---|
| `MigrationTests` | la migration initiale crée le schéma, aucune migration en attente | migration et snapshot cohérents, cinq tables |
| `CommandeRepositoryTests` | ajouter puis relire charge le client et les lignes | agrégat complet, total 3 090,00 (200 × 18,50 et 10 × 42 à 25 %) |
| | le numéro est séquentiel par année | `CMD-2026-000001`, `000002`, `CMD-2027-000001` |
| | lister par statut ne suit pas les entités | `AsNoTracking()` |
| | une commande sans stock passe en attente de stock | règle métier traversant la persistance |
| | le stock par entrepôt est persisté dans sa propre table | `OwnsMany` |
| | annuler une commande en préparation est refusé par le domaine | `RegleMetierException` après relecture |
| | `ExecuteUpdate` facture en masse les commandes expédiées | mise à jour sans chargement |
| `ConcurrenceOptimisteTests` | modifier une commande renouvelle son jeton | `Version` change |
| | modifier les métadonnées JSON renouvelle aussi le jeton | owned type couvert |
| | deux modifications concurrentes : la seconde échoue | `DbUpdateConcurrencyException`, `ReloadAsync` |
| `MetadonneesJsonTests` | les métadonnées sont stockées en JSON et relues à l'identique | colonne brute + relecture |
| | on peut filtrer sur une propriété JSON en LINQ | traduction SQL |
| `CommandeLectureDapperTests` | la lecture Dapper agrège les lignes et le montant par commande du jour | 2 lignes, 1 513,50 |

## Passer à SQL Server / Azure SQL

1. Dans `Textinord.Trame.Infrastructure.csproj`, décommenter `Microsoft.EntityFrameworkCore.SqlServer` (déjà dans `Directory.Packages.props`).
2. Dans `DependencyInjection.AddTramePersistence` et `TrameDbContextFactory`, remplacer `UseSqlite` par le bloc `UseSqlServer` en commentaire (avec `EnableRetryOnFailure()` pour Azure SQL).
3. Régénérer la migration : les types changent (`INTEGER` → `int`, `TEXT` → `nvarchar`, `decimal(18,2)`) ; `dotnet dotnet-ef migrations remove` puis `migrations add Initiale`. Une migration n'est pas portable d'un provider à l'autre.
4. Optionnel : remplacer le jeton applicatif par `rowversion`.
5. Les tests d'intégration restent sur SQLite ; on ajoute en CI une exécution sur SQL Server (Testcontainers si Docker, sinon LocalDB) pour les requêtes sensibles au dialecte (JSON, `decimal`).

## Variantes acceptables

1. Jeton `int` incrémenté à chaque sauvegarde plutôt que `Guid` : même mécanique, plus compact ; vérifier que l'incrément est fait dans `SaveChangesAsync`, pas dans le domaine.
2. `Version` gérée par un `SaveChangesInterceptor` plutôt qu'une surcharge de `SaveChangesAsync` : plus propre pour une solution qui aura plusieurs contextes ; acceptable.
3. Repository qui expose `Task<Commande?> ObtenirAsync(int id)` uniquement et une classe de requêtes séparée pour `ListerParStatutAsync` : c'est déjà la séparation lecture / écriture ; acceptable et même encouragé.
4. `EnsureCreated()` dans les tests au lieu de `Migrate()` : fonctionne, mais ne teste pas la migration ; demander de passer à `Migrate()`.

## Erreurs classiques à repérer en correction

| Erreur observée | Cause probable | Comment corriger |
|---|---|---|
| Migration retouchée à la main (colonne ajoutée dans `Up`) après un oubli de configuration | réflexe « c'est plus rapide » | `migrations remove`, corriger la configuration, `migrations add` : le snapshot doit correspondre au modèle |
| `[Timestamp] byte[] RowVersion` dans le domaine | copie d'un exemple SQL Server | ne marche pas sur SQLite (la valeur reste nulle) ; repasser au jeton applicatif ou isoler la variante SQL Server dans la configuration |
| `SaveChanges` appelé dans le repository | habitude des repositories génériques | l'unité de travail décide du commit ; sortir l'appel vers le cas d'usage / le test |
| Deux contextes sur deux connexions `:memory:` distinctes | on croit partager la base | une base `:memory:` est privée à sa connexion ; une seule `SqliteConnection` par test, plusieurs contextes dessus |
| Test de concurrence qui charge la seconde commande après la première sauvegarde | ordre des lectures | les deux lectures doivent précéder la première écriture, sinon la seconde voit déjà la version B |
| Dapper : montant `0` ou exception de conversion | agrégat lu en `decimal` depuis un `REAL` SQLite | record intermédiaire en `double`, conversion explicite |

## Points à insister en débriefing

- La persistance est un détail d'implémentation du point de vue du domaine, mais un détail qui a des règles : intégrité et index dans la base, décisions dans le domaine, transaction pilotée par l'unité de travail.
- Le test de conflit vaut plus que tout le reste du TP : c'est celui qui évite, en production, la commande annulée après son départ en préparation.
- SQLite en mémoire n'est pas un compromis honteux : c'est ce qui permet 14 tests en moins d'une seconde, sans Docker ni serveur. La CI ajoutera SQL Server pour le dialecte.
- Lien avec la suite : le TP 5 expose ce repository derrière une Minimal API ; le TP 6 ajoute l'Outbox dans la même transaction que `SaveChangesAsync`.

## Bonus

- `AsSplitQuery()` sur `ObtenirAsync` : justifié seulement si une seconde collection (par exemple les ordres de préparation) est incluse ; avec une seule collection, la jointure unique reste plus efficace.
- Requête compilée pour `ObtenirParNumeroAsync` : `EF.CompileAsyncQuery((TrameDbContext db, string numero) => db.Commandes.Include(c => c.Client).Include(c => c.Lignes).FirstOrDefault(c => c.Numero == numero))`.
- Script idempotent : `dotnet dotnet-ef migrations script --idempotent -o deploy/trame2.sql --project src/Textinord.Trame.Infrastructure` ; à lire avec le groupe : on y voit la table `__EFMigrationsHistory` et les `IF NOT EXISTS`.
