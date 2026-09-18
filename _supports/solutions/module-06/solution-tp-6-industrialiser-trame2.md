# Solution — TP 6 : Industrialiser Trame 2

> Document formateur — Ne pas distribuer avant la fin du TP.

Le code complet se trouve dans `solutions/module-06/tp-6/`. Il compile sans avertissement et ses 34 tests passent sur macOS, Linux et Windows avec le SDK .NET 10 (10.0.302 utilisé pour la vérification), sans Docker, sans RabbitMQ, sans Azure.

```bash
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
cd solutions/module-06/tp-6
dotnet build      # La génération a réussi. 0 Avertissement(s) 0 Erreur(s)
dotnet test       # Réussi ! total : 34 (Domain 14, Api 12, Worker 8)
```

## Approche pédagogique

Le TP est le point de convergence des six modules : le noyau métier du module 1, la structure de solution du module 2, la persistance du module 4, la Minimal API du module 5, et les trois sujets propres à ce module — Outbox, tests, pipeline. En séance, vous n'avez que 35 minutes dans le découpage des notes formateur (jusqu'à 60 si le groupe est en avance) : faites tenir les étapes 1 à 3 (Outbox, API, Worker), lancez l'étape 4 (tests) si le temps le permet, et présentez les étapes 5 à 7 sur la solution, en montrant les fichiers plutôt qu'en les faisant écrire. Les apprenants qui finissent les étapes 1 à 3 en 25 minutes ont un binôme à aider.

Le point de contrôle 3 (arrêter le Worker, poster trois commandes, relancer, rejouer) est celui qui fait comprendre l'Outbox mieux que toute slide : ne le sautez pas.

## Structure de la solution

```
tp-6/
  Textinord.Trame2.sln
  global.json                     SDK 10.0.100, rollForward latestFeature
  Directory.Build.props           net10.0, nullable, warnings = erreurs, CPM, Nerdbank.GitVersioning
  Directory.Packages.props        une version par package, correctifs de sécurité épinglés
  version.json                    2.1-beta, branches release/vX.Y
  azure-pipelines.yml             Build → Test → Publish → Deploy_Recette → Deploy_Production
  .github/workflows/ci.yml        équivalent GitHub Actions
  CHANGELOG.md                    notes de release (Keep a Changelog)
  .editorconfig, .gitignore
  src/
    Textinord.Trame.Domain/       Commande, LigneCommande, RegleRemise, NumeroCommande, Resultat<T>,
                                  Client, Article, StockArticle, OrdrePreparation, Events/CommandeValidee,
                                  Services/IStockDisponible — aucun package
    Textinord.Trame.Infrastructure/
                                  TrameDbContext (EF Core 10, SQLite), Outbox/OutboxMessage,
                                  Outbox/OutboxSerialiseur, StockParTable, DonneesInitiales,
                                  InitialisationBase (IHostedService), AddTrameInfrastructure
    Textinord.Trame.Api/          Program.cs (Minimal API, OpenAPI, ProblemDetails, /version),
                                  Commandes/PriseDeCommande, CommandesEndpoints, Contrats
    Textinord.Trame.Worker/       Program.cs, MessagingConfiguration (InMemory | RabbitMq),
                                  Outbox/OutboxRelay, OutboxRelayService, OutboxOptions,
                                  Consumers/OrdrePreparationConsumer
  tests/
    Directory.Build.props         xUnit, NSubstitute, coverlet, usings globaux pour tous les tests
    Textinord.Trame.Domain.Tests/ RegleRemiseTests (6), CommandeTests (8)
    Textinord.Trame.Worker.Tests/ SqliteEnMemoire, OutboxRelayTests (4), OrdrePreparationConsumerTests (2),
                                  BusEnMemoireTests (2, test harness MassTransit)
    Textinord.Trame.Api.Tests/    TrameApiFactory, CommandesEndpointsTests (9), PriseDeCommandeTests (3)
```

## Les choix expliqués

### Étape 1 — Persistance et Outbox

**Une table Outbox écrite à la main.** MassTransit fournit `AddEntityFrameworkOutbox`, plus complet (Inbox, relais intégré, nettoyage). Le TP l'écrit pour que les apprenants voient les quarante lignes qui font la garantie : une ligne par événement, un `MessageId` généré à l'écriture, un marqueur `EnvoyeLe`, un compteur de tentatives. En production Textinord, on prendra l'Outbox MassTransit ; on saura ce qu'elle fait.

**Le registre de types explicite** (`OutboxSerialiseur.TypesConnus`) : la colonne `Type` contient un nom court, jamais un nom qualifié d'assembly. `Type.GetType(chaine)` sur une valeur venue de la base est une porte d'entrée classique pour la désérialisation arbitraire ; le dictionnaire ferme cette porte et rend explicite la liste des messages que l'Outbox transporte.

**`EnsureCreatedAsync` plutôt que des migrations** : suffisant pour un TP dont la base est jetable ; en production, les migrations du module 4 s'appliquent dans le pipeline (`dotnet ef database update` ou script SQL idempotent), jamais au démarrage de l'application. `InitialisationBase` active aussi `PRAGMA journal_mode=WAL` : sans lui, l'API et le Worker se bloquent mutuellement (`database is locked`) sur le même fichier SQLite.

**La collection `Lignes` en lecture seule** (`IReadOnlyList<LigneCommande>` exposée, champ `_lignes` mappé par `HasField` et `PropertyAccessMode.Field`) : le modèle du module 1 est respecté, personne ne peut ajouter une ligne sans passer par le domaine, et EF Core hydrate quand même la collection.

**L'index unique `(CommandeId, Entrepot)`** sur les ordres de préparation : c'est la clé d'idempotence, gravée dans le schéma. Le consumer vérifie avant d'insérer ; si deux instances insèrent en parallèle, la base tranche et le consumer attrape la `DbUpdateException`.

### Étape 2 — L'API transactionnelle

Le cœur du TP tient en quatre lignes de `PriseDeCommande.EnregistrerAsync` :

```csharp
db.Commandes.Add(commande);
if (evenement is not null)
{
    db.Outbox.Add(OutboxSerialiseur.Emballer(evenement, maintenant));
}
await db.SaveChangesAsync(ct);
```

Un seul `SaveChangesAsync` : EF Core ouvre une transaction, écrit la commande, ses lignes, le compteur de numérotation et la ligne Outbox, puis valide. Aucune transaction explicite n'est nécessaire, et c'est le message à faire passer : le cas normal ne demande **rien** de plus. La transaction explicite (`BeginTransactionAsync`) n'apparaît que s'il y a plusieurs `SaveChanges` ou du Dapper sur la même connexion (slide « `TransactionScope` local, EF Core 10 et sagas »).

**La règle de stock est dans le domaine**, pas dans l'API : `Commande.Valider(affectations, maintenant)` reçoit, pour chaque référence, l'entrepôt qui peut servir la ligne (ou `null`), et décide : `Validee` avec l'événement, ou `EnAttenteStock` sans événement. L'API se contente d'interroger `IStockDisponible` ligne par ligne. Conséquence testable : une commande en attente de stock ne produit **aucune** ligne Outbox (test `Post_commande_en_rupture_passe_en_attente_stock_sans_message_outbox`).

**La numérotation `CMD-AAAA-NNNNNN`** utilise une table `CompteursCommandes` (un enregistrement par année) lue et incrémentée dans la même transaction. Simplification assumée : deux requêtes strictement simultanées peuvent lire le même compteur ; l'index unique sur `Numero` fait alors échouer la seconde, qui doit être rejouée par le client. En production sur SQL Server ou Azure SQL, on remplace le compteur par une `SEQUENCE` (`modelBuilder.HasSequence<int>("SeqCommandes2026")`), atomique par construction. À dire en débriefing si un apprenant pose la question ; ne pas le corriger dans le TP.

**`ValidationProblem` (400) pour toutes les erreurs attendues** : client inconnu, article inconnu, quantité nulle, aucune ligne. Le domaine renvoie un `Resultat<Commande>`, jamais une exception pour un cas prévu ; l'exception est réservée à l'imprévu (et `UseExceptionHandler` la transforme en 500 ProblemDetails).

**`public partial class Program;`** en fin de `Program.cs` : indispensable pour `WebApplicationFactory<Program>`. C'est la ligne que les apprenants oublient le plus souvent.

### Étape 3 — Le Worker

**`MessagingConfiguration.AddTrameMessaging`** : un seul point de configuration du bus, le transport choisi par `Messaging:Transport`. Le helper `ConfigurerEndpoints<TEndpoint>` est générique parce que `ConfigureEndpoints` est une méthode d'extension sur `IBusFactoryConfigurator<T>` — sans le paramètre de type, le compilateur refuse d'inférer (erreur `CS0411`, rencontrée en construisant la solution et consignée dans le dépannage du TP). `UseMessageRetry(r => r.Intervals(1 s, 5 s, 30 s))` s'applique aux deux transports.

**`OutboxRelay`** est une classe scoped, séparée du `BackgroundService`, pour être testable sans hôte : on lui injecte un `IPublishEndpoint` (substitué dans `OutboxRelayTests`, réel dans `BusEnMemoireTests`). Le `MessageId` de la ligne Outbox est transmis au bus par `Pipe.Execute<PublishContext>(ctx => ctx.MessageId = message.MessageId)`. Le marquage `EnvoyeLe` a lieu **après** la publication : si le processus tombe entre les deux, le message est republié au cycle suivant — garantie « au moins une fois », assumée.

**`OutboxRelayService`** crée un scope DI par cycle (un `DbContext` par cycle, jamais un `DbContext` capturé par le singleton — la dépendance captive du module 5), attrape toute exception de cycle pour que le Worker survive à une base indisponible, et sort proprement sur `OperationCanceledException`.

**`OrdrePreparationConsumer`** lit d'abord les entrepôts déjà servis pour la commande, ne crée que les manquants, et traite la violation d'index unique comme un succès silencieux. Trois tests le couvrent : un ordre par entrepôt, trois consommations du même message = deux ordres, et le harness qui publie deux fois le même événement.

**`OrdrePreparation.Depuis(...)`** : une fabrique statique dans le domaine, pour que le consumer ne connaisse pas la façon de compter lignes et pièces. C'est aussi ce qui permet au snippet de la slide « MassTransit, Wolverine, NServiceBus » de tenir en douze lignes.

### Étape 4 — Les tests

| Projet | Classe | Ce qui est vérifié | Technique |
|---|---|---|---|
| Domain.Tests | `RegleRemiseTests` (6) | cumul client + volume, plafond 30 %, taux hors bornes | théorie à cinq cas, `Assert.Throws` |
| Domain.Tests | `CommandeTests` (8) | refus sans ligne, numéro mal formé, format du numéro, validation avec affectation et événement, attente de stock, montant net, annulation avant et après préparation | xUnit pur, `Assert.Collection` |
| Worker.Tests | `OutboxRelayTests` (4) | publie et marque envoyés, ne republie pas, compte les tentatives sur échec, abandonne au-delà du maximum | `IPublishEndpoint` et `TimeProvider` substitués (NSubstitute), SQLite en mémoire |
| Worker.Tests | `OrdrePreparationConsumerTests` (2) | un ordre par entrepôt, idempotence sur trois rejeux | `ConsumeContext<CommandeValidee>` substitué |
| Worker.Tests | `BusEnMemoireTests` (2) | relais → bus → consumer → base ; même événement livré deux fois = un ordre par entrepôt | `AddMassTransitTestHarness`, `harness.Published`, `harness.Consumed`, `InactivityTask` |
| Api.Tests | `CommandesEndpointsTests` (9) | 201 + Outbox dans la même base, rupture sans Outbox, remise plafonnée, 400 ProblemDetails (sans ligne, client inactif), 404, GET avec lignes, `/version`, `/diagnostic/outbox` | `WebApplicationFactory<Program>`, SQLite temporaire par classe |
| Api.Tests | `PriseDeCommandeTests` (3) | interrogation du stock par ligne, numérotation séquentielle, article inconnu sans appel au stock | `IStockDisponible` substitué, SQLite en mémoire, données initiales réelles |

Sortie attendue de `dotnet test` (ordre des projets variable) :

```text
Réussi!  - échec :     0, réussite :    14, ignorée(s) :     0, total :    14 - Textinord.Trame.Domain.Tests.dll (net10.0)
Réussi!  - échec :     0, réussite :    12, ignorée(s) :     0, total :    12 - Textinord.Trame.Api.Tests.dll (net10.0)
Réussi!  - échec :     0, réussite :     8, ignorée(s) :     0, total :     8 - Textinord.Trame.Worker.Tests.dll (net10.0)
```

Deux détails d'implémentation des tests méritent d'être montrés :

- `SqliteEnMemoire` garde une `SqliteConnection` ouverte sur `Data Source=:memory:` : la base vit tant que la connexion vit, et chaque test en crée une, isolée. Le même pattern sert dans `PriseDeCommandeTests`.
- `TrameApiFactory` surcharge la chaîne de connexion par `builder.UseSetting("ConnectionStrings:Trame", ...)` vers un fichier temporaire propre à la classe de tests, et le supprime à la fin (`SqliteConnection.ClearAllPools()` d'abord, sinon le fichier reste verrouillé). L'initialisation de la base est celle de l'application (`InitialisationBase`), pas une copie dans les tests.

Une observation faite en construisant la solution, à partager : avec le transport en mémoire, deux publications portant le **même** `MessageId` ne produisent qu'une consommation — le transport déduplique, comme Service Bus. Le test du harness publie donc le même événement avec deux identifiants distincts, ce qui est le cas défavorable réel (relais tombé entre `Publish` et le marquage, puis relancé). L'idempotence du consumer est la seule garantie qui ne dépend d'aucun broker.

### Étape 5 — Les pipelines

`azure-pipelines.yml`, stage par stage :

| Stage | Ce qu'il fait | Points à commenter |
|---|---|---|
| `Build` | `checkout` avec `fetchDepth: 0`, `UseDotNet@2`, cache NuGet, `nbgv cloud`, restore, build Release | sans l'historique Git complet, Nerdbank.GitVersioning ne peut pas calculer la hauteur ; `nbgv cloud` renseigne `Build.BuildNumber` avec la version SemVer |
| `Test` | `DotNetCoreCLI@2` avec `--collect:"XPlat Code Coverage" --logger trx`, `publishTestResults: true`, `PublishCodeCoverageResults@2` | les résultats apparaissent dans l'onglet Tests, la couverture dans Code Coverage ; un test rouge arrête le pipeline ici |
| `Publish` | `dotnet publish` de l'API et du Worker en zip, copie du `CHANGELOG.md`, `PublishPipelineArtifact@1`, puis `dotnet publish -t:PublishContainer` vers Azure Container Registry | `condition: ne(variables['Build.Reason'], 'PullRequest')` : une PR s'arrête aux tests ; pas de Dockerfile, l'image est produite par le SDK |
| `Deploy_Recette` | `deployment` job, `environment: trame2-recette`, `AzureContainerApps@1` pour l'API et le Worker, test de fumée `curl` sur l'OpenAPI | automatique sur `main` et `release/*` ; l'environnement trace qui a déployé quoi |
| `Deploy_Production` | `environment: trame2-production`, condition `startsWith(..., 'refs/heads/release/')`, stratégie `runOnce` avec `deploy`, `routeTraffic` (canary 10 %), `postRouteTraffic` (surveillance 5 minutes puis 100 %), `on: failure:` (rollback à 100 % sur la révision stable) | **l'approbation n'est pas dans le YAML** : elle est configurée sur l'environnement (Pipelines > Environments > Approvals and checks : Awa Diop, Sofia Marques). Le YAML dit quoi, l'environnement dit qui a le droit |

Les secrets ne sont jamais dans le fichier : la service connection `textinord-azure` porte l'identité Azure, le groupe de variables `trame2-secrets` porte les noms de ressources, la connexion Docker `textinord-acr` porte les identifiants du registre.

`.github/workflows/ci.yml` reproduit la même chaîne avec quatre jobs : `build-test` (avec `dotnet/nbgv@v0.4` pour exposer la version en sortie de job, `dorny/test-reporter` pour les TRX, `irongut/CodeCoverageSummary` pour la couverture), `publish` (artefact nommé avec la version, images vers `ghcr.io`), `deploy-recette` (`environment: recette`, connexion Azure par OIDC sans mot de passe stocké), `deploy-production` (`environment: production` avec reviewers requis dans Settings > Environments, canary, rollback `if: failure()`). Les différences à souligner : pas de `deployment` job ni de stratégie `runOnce` côté GitHub, l'environnement porte l'URL et les reviewers, le rollback est une étape conditionnelle.

Les deux fichiers sont syntaxiquement valides (chargés sans erreur par un analyseur YAML : `ruby -ryaml -e 'YAML.safe_load(File.read("azure-pipelines.yml"))'`, ou PyYAML si disponible) ; ils n'ont pas été exécutés sur un agent — dites-le, et montrez où il faudrait créer les environnements, la service connection et les secrets avant la première exécution.

### Étape 6 — Version et notes de release

`version.json` fixe `"version": "2.1-beta"` : chaque build calcule `2.1.<hauteur>-beta` sur `main` (pré-version) et `2.1.<hauteur>` sur `release/v2.1` (version publique, grâce à `publicReleaseRefSpec`). La section `release` documente `nbgv prepare-release` : elle crée `release/v2.1` et passe `main` en `2.2-beta`. Hors dépôt Git — le cas du dossier `solutions/` — la hauteur est nulle et l'assembly est estampillé `2.1.0-beta` ; c'est ce que renvoie `GET /version`, et ce que vérifie le test `Version_expose_le_numero_semver_calcule_par_nerdbank_gitversioning` (`StartsWith("2.1.")`).

`Directory.Build.props` référence `Nerdbank.GitVersioning` avec `PrivateAssets="all"` (outil de build, jamais une dépendance de l'application) ; la version du package vient de `Directory.Packages.props` comme toutes les autres. `ContinuousIntegrationBuild` est activé quand `TF_BUILD` ou `GITHUB_ACTIONS` est défini : chemins normalisés dans les PDB, build reproductible.

`CHANGELOG.md` suit Keep a Changelog : une section « Non publié » alimentée à chaque pull request, des sections datées par version, des liens de comparaison en bas, un identifiant de work item par ligne. La règle d'écriture est en tête du fichier : c'est Awa Diop qui le lit aux entrepôts, pas un développeur.

### Ce qui a été volontairement laissé de côté

- Pas d'authentification sur l'API (module 5 la couvre) ; pas d'Inbox MassTransit (l'idempotence par clé naturelle suffit ici).
- Pas de migrations EF Core, pas de `SEQUENCE` pour la numérotation (voir ci-dessus).
- Azure Service Bus n'est pas configuré : le package `MassTransit.Azure.ServiceBus.Core` s'ajoute avec un troisième cas dans `MessagingConfiguration` (`UsingAzureServiceBus`), sans changer le consumer.
- MassTransit reste en 8.5.x : dernière branche Apache 2.0 ; la v9 est sous licence commerciale, ce qui est un critère d'architecture à part entière (à dire).
- L'audit NuGet a bloqué la première restauration (`NU1903` sur `SQLitePCLRaw.lib.e_sqlite3` et `Microsoft.OpenApi`, dépendances transitives) : les versions corrigées sont épinglées dans `Directory.Packages.props` et référencées dans `Infrastructure` et `Api`. C'est un incident réel, pédagogiquement précieux : la chaîne d'approvisionnement fait partie de l'usine logicielle.

## Dépannage

| Symptôme | Cause | Correction |
|---|---|---|
| `error NU1903` à la restauration | audit NuGet + `TreatWarningsAsErrors` | épingler une version corrigée du package transitif dans `Directory.Packages.props` et le référencer dans le projet concerné ; ne pas désactiver l'audit |
| `CS0411` sur `ConfigureEndpoints` | helper non générique | `ConfigurerEndpoints<TEndpoint>(IBusRegistrationContext, IBusFactoryConfigurator<TEndpoint>) where TEndpoint : IReceiveEndpointConfigurator` |
| `dotnet new sln` crée un `.slnx` | SDK .NET 10 : nouveau format par défaut | `dotnet new sln --format sln` ; le `.slnx` fonctionne aussi, mais la convention du dépôt est `.sln` |
| `database is locked` | journal SQLite non WAL, ou `DbContext` partagé | `PRAGMA journal_mode=WAL` dans `InitialisationBase` ; un scope par cycle de relais |
| Test du harness qui échoue avec « 1 consommé au lieu de 2 » | même `MessageId` forcé sur deux publications : le transport déduplique | publier avec des identifiants distincts ; c'est le cas réel d'un relais relancé |
| `WebApplicationFactory` ne trouve pas `Program` | `public partial class Program;` absent, ou `Api.Tests` sans référence à `Api` | ajouter la ligne en fin de `Program.cs` et la référence de projet |
| Les tests d'intégration laissent des fichiers `trame2-tests-*.db` dans le dossier temporaire | `Dispose` non appelé (test interrompu) | `SqliteConnection.ClearAllPools()` puis suppression dans `Dispose(bool)` ; nettoyer `$TMPDIR` si besoin |
| `GET /version` renvoie `1.0.0` | `version.json` absent, ou Nerdbank.GitVersioning non référencé | vérifier le `PackageReference` dans `Directory.Build.props` et l'emplacement du fichier à la racine |
| Le Worker relaie mais journalise `Type de message Outbox inconnu` | événement non enregistré dans `OutboxSerialiseur.TypesConnus` | ajouter le type au registre : c'est volontairement explicite |

## Grille de correction : ce que l'on regarde concrètement

- **Transaction (4 pts)** : un seul `SaveChangesAsync` dans le cas d'usage ; le test « rupture sans Outbox » existe et passe ; aucune publication directe depuis l'API.
- **Relais (3 pts)** : lot ordonné par `Id`, `MessageId` transmis au bus, `EnvoyeLe` après publication, tentatives comptées, `TentativesMaximum` respecté, exception de cycle journalisée sans arrêt du Worker.
- **Consumer (3 pts)** : lecture des entrepôts déjà servis, index unique en base, `DbUpdateException` traitée ; `Messaging:Transport = RabbitMq` accepté par la configuration.
- **Tests (4 pts)** : dix minimum ; au moins un substitut NSubstitute sur un port (`IPublishEndpoint`, `IStockDisponible`, `ConsumeContext<T>`) ; `WebApplicationFactory` sur SQLite temporaire ; un test de harness. Retirer un point si les tests d'intégration partagent une base entre classes (ordre-dépendance).
- **Pipelines (3 pts)** : cinq stages commentés, résultats et couverture publiés, `Publish` conditionné hors PR, `environment` sur les deployment jobs, condition de branche pour la production, workflow GitHub équivalent avec `environment:`.
- **Version et release (2 pts)** : `version.json` avec `publicReleaseRefSpec`, `PrivateAssets="all"`, `GET /version` qui répond `2.1.…`, `CHANGELOG.md` avec sections Ajouté / Modifié / Technique et identifiants de work items.
- **Qualité du dépôt (1 pt)** : `Directory.Build.props` et `Directory.Packages.props` utilisés (aucun `Version=` dans les `.csproj`), `.gitignore`, aucun `bin/` ni `obj/`, build sans avertissement.
