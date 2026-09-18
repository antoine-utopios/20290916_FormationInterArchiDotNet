# Solution — TP 2 : squelette de Trame 2 et bus interne

Code complet dans `solutions/module-02/tp-2/` : solution `Textinord.Trame.sln`, .NET 10, C# 14.

```bash
cd solutions/module-02/tp-2
dotnet build          # 0 avertissement, 0 erreur (analyzers latest-recommended, TreatWarningsAsErrors)
dotnet test           # 26 tests : 8 Domain.Tests + 18 Application.Tests
dotnet run --project src/Textinord.Trame.Worker     # simulation : 3 validations, 4 ordres, 2 dead letters, arrêt propre
dotnet run --project src/Textinord.Trame.Api        # http://localhost:5210/openapi/v1.json
```

## Structure livrée

```text
Textinord.Trame.sln
Directory.Build.props      net10.0, Nullable, TreatWarningsAsErrors, AnalysisLevel latest-recommended,
                           EnforceCodeStyleInBuild, RootNamespace = AssemblyName = nom du projet,
                           section conditionnelle *.Tests (xunit, Test.Sdk, runner, coverlet, Using Xunit)
Directory.Packages.props   Microsoft.Extensions.* 10.0.11, Microsoft.AspNetCore.OpenApi 10.0.11,
                           xunit 2.9.3, Microsoft.NET.Test.Sdk 17.14.1, runner 3.1.4, coverlet 6.0.4
.editorconfig              namespaces file-scoped, var, règles de nommage en warning (I, Async, _camelCase,
                           PascalCase, camelCase), CA1848 en warning, section [tests/**.cs]
global.json                10.0.100, rollForward latestFeature
src/Textinord.Trame.Domain            Commande, LigneCommande, NumeroCommande, StatutCommande, IDisponibiliteStock,
                                      ConditionTarifaire, PolitiqueRemise, Client, Article, Entrepot
src/Textinord.Trame.Application       Messaging/ (MessageEnvelope, MessageChannel, IMessageBus, InProcessMessageBus,
                                      IMessageContext, MessageHandler, ContentBasedRouter, IMessageTranslator,
                                      DeadLetterChannel, HistoriqueMessages, WireTap, exceptions)
                                      Commandes/ (CanauxTrame, Messages/, Routage/, Traduction/, Pipeline/,
                                      ICommandeRepository, IEntrepotGateway, ValiderCommandeHandler)
                                      DependencyInjection/AddTrameApplication
src/Textinord.Trame.Infrastructure    InMemoryCommandeRepository + JeuDeDemonstration, StockEnMemoire,
                                      JournalEntrepotGateway, MessageBusHostedService, AddTrameInfrastructure
src/Textinord.Trame.Api               Minimal API : /commandes, /commandes/{numero}/validation,
                                      /messages/commandes-validees, /ordres, /dead-letters, /audit/{correlationId}, /canaux
src/Textinord.Trame.Worker            SimulationCommandesWorker (BackgroundService)
tests/Textinord.Trame.Domain.Tests    PolitiqueRemiseTests (4), CommandeTests (4)
tests/Textinord.Trame.Application.Tests
                                      ContentBasedRouterTests (4), OrdrePreparationTranslatorTests (3),
                                      MessageEnvelopeTests (2), InProcessMessageBusTests (5), PipelineCommandesTests (4)
                                      Outils/ : HorlogeFixe (TimeProvider), EntrepotGatewayEnMemoire, Fabrique
docs/architecture.md, tools/README.md, .gitignore
```

## Les choix, et pourquoi

### Squelette et conventions

- **Un projet par couche, références vers le centre.** `Domain` ne référence rien ; `Application` référence
  `Domain` et seulement deux packages d'abstractions (`DependencyInjection.Abstractions`,
  `Logging.Abstractions`) ; `Infrastructure` implémente les ports d'`Application` ; `Api` et `Worker` sont des
  **hôtes** : ils assemblent, ils ne décident pas. C'est la structure de la slide 32, et c'est celle que le
  module 4 (EF Core dans `Infrastructure`) et le module 6 (MassTransit dans `Infrastructure`) rempliront.
- **`Directory.Build.props` avec une section conditionnelle sur le nom du projet.** Tout projet dont le nom
  finit par `.Tests` reçoit les packages xUnit et `<Using Include="Xunit" />` : le `.csproj` d'un projet de
  tests ne contient plus qu'une `ProjectReference`. Un nouveau projet de tests se crée en trois lignes.
- **`latest-recommended` + `TreatWarningsAsErrors`.** C'est exigeant ; deux règles ont demandé une décision :
  `CA1848` (journalisation via `LoggerMessage`) que l'on **impose** parce que le source generator est le bon
  réflexe en 2026, et `CA1716` (noms réservés dans d'autres langages) que l'on désactive parce que le code est
  en français. `CA1873` (argument de log coûteux) a été corrigé en calculant les valeurs avant l'appel.
- **Règles de nommage en `warning`, donc en erreur.** Piège vérifié : la sévérité écrite dans
  `dotnet_naming_rule.*.severity` n'est lue que par l'IDE ; pour que `dotnet build` échoue, il faut aussi
  `dotnet_diagnostic.IDE1006.severity = warning`. La section `[tests/**.cs]` relâche le suffixe `Async` et les
  underscores : un nom de test décrit un comportement, il ne suit pas les règles de l'API publique.
- **`global.json` en `latestFeature`** : tout SDK 10.0.x convient, la CI et les postes ne se bloquent pas
  sur un numéro de patch.

### Le bus

- **`Channel<MessageEnvelope>` borné, `FullMode = Wait`, `SingleReader = true`.** Un producteur trop rapide
  attend (contre-pression) ; une seule boucle de lecture par canal, ce qui rend le dispatch séquentiel et
  prévisible. La capacité par défaut (1 000) couvre une pointe de 900 commandes par jour avec de la marge.
- **Une enveloppe non générique (`Corps` en `object`, `Type` en texte).** Un canal transporte des enveloppes
  hétérogènes (le routeur fait passer une `CommandeValidee`, le splitter dérive un `OrdrePreparationEntrepot`).
  `CorpsEnTantQue<T>()` rétablit le typage côté handler, avec un message d'erreur explicite.
- **`Deriver<T>` pour propager la corrélation.** Un handler ne remplit jamais `CorrelationId` à la main :
  `contexte.PublierAsync(canal, corps)` dérive le message courant, conserve la corrélation, l'expiration et
  l'adresse de retour, et pose `CausationId`. La corrélation « ne peut pas » être perdue.
- **Le routeur publie la même enveloppe** (pas de dérivation) : un routeur ne transforme pas. Le splitter, lui,
  dérive une enveloppe par ordre et pose la séquence `AvecSequence(i, n)`.
- **Traduire avant de publier.** `SplitterParEntrepot` traduit tous les entrepôts puis publie : si un entrepôt
  est inconnu, rien n'est parti et le message d'origine part entier en dead letter. C'est une forme minimale
  d'atomicité ; la vraie réponse (Outbox) est au module 6.
- **Arrêt amont → aval.** `ArreterAsync` ferme les canaux **dans l'ordre de déclaration** et attend chaque
  boucle avant de fermer la suivante : un message en cours de traitement trouve toujours son canal aval ouvert.
  C'est pour cela que `PipelineCommandes` déclare `commandes.validees` avant `preparations.*` avant
  `entrepot.*`. Le test `Les_canaux_sont_declares_de_l_amont_vers_l_aval` protège cet invariant.
- **`TimeProvider` injecté.** L'expiration se teste en avançant une `HorlogeFixe` de six minutes, sans
  `Task.Delay`. Le bus, le dépôt et la passerelle datent tout par le même `TimeProvider`.
- **Trois sorties de secours, jamais une perte** : message expiré, canal sans abonné, exception de handler.
  Chaque dead letter porte le canal, la raison et l'enveloppe complète, et `DeadLetterChannel.AttendreAsync`
  permet à la supervision (ou à un test) d'attendre la suivante.
- **Wire tap et historique.** `WireTap.Vers("audit")` est abonné **en premier** sur `commandes.validees` et sur
  chaque canal d'entrepôt : un message qui échouera ensuite est quand même audité. `HistoriqueMessages` est un
  Message Store borné, consultable par corrélation (`GET /audit/{correlationId}`) et trié par date d'émission
  puis numéro de séquence.

### Les hôtes

- `MessageBusHostedService` résout `PipelineCommandes` (qui déclare canaux et abonnements dans son
  constructeur) puis démarre le bus ; à l'arrêt de l'hôte, il vide les canaux. `ArreterAsync` est idempotent :
  l'hôte l'appelle, puis `DisposeAsync` l'appelle encore, sans effet.
- Le **Worker** simule une matinée d'ADV : trois validations via `ValiderCommandeHandler` (chacune dans un scope
  DI), un message sans pays et un message avec un entrepôt inconnu (deux dead letters attendues), l'historique
  de chaque corrélation, puis `StopApplication()`. Il sert de test de fumée exécutable en cinq secondes.
- L'**API** expose le même pipeline derrière HTTP. `POST /messages/commandes-validees` est un Channel Adapter
  d'entrée : un producteur externe (Trame, EDI) dépose directement un événement canonique, ce qui permet de
  montrer le routeur, le traducteur et la dead letter sans passer par le domaine.

## Ce qui change aux modules suivants

| Module | Remplacement | Ce qui ne bouge pas |
|---|---|---|
| 4 — Persistance | `InMemoryCommandeRepository` → EF Core sur SQLite / Azure SQL ; `HistoriqueMessages` → table d'audit | `ICommandeRepository`, `ValiderCommandeHandler` |
| 5 — API | JWT, versioning, ProblemDetails, tests `WebApplicationFactory` sur ces endpoints | le pipeline |
| 6 — Messaging | `InProcessMessageBus` → MassTransit (transport in-memory puis RabbitMQ / Service Bus), Outbox EF Core, saga d'expédition | `IMessageBus`, les handlers, `ContentBasedRouter`, `OrdrePreparationTranslator`, les tests du routeur et du traducteur |

## Écarts acceptables dans les copies

- Un bus sans `TimeProvider` (expiration testée avec `DateTimeOffset.UtcNow` et une durée négative) : accepté si
  le test est déterministe.
- Pas de wire tap ni d'historique : ce sont des bonus.
- Un `Worker` qui tourne en boucle sans s'arrêter : accepté, mais faire remarquer l'intérêt de
  `IHostApplicationLifetime.StopApplication()` pour un outil de démonstration.
- Des noms anglais cohérents (`OrderValidated`, `WarehouseGateway`) : acceptés ; ce qui ne l'est pas, c'est le
  mélange dans un même type.
