# TP 2 — Squelette de Trame 2 et bus interne

**Module 2 — Intégration par messages et plateforme .NET · 75 min · binômes**
Slide de renvoi : 35. Supports utiles : slides 11, 12, 16, 17, 21, 31, 32, 33 et la démo 2.1.

## Mise en situation

Le noyau métier existe depuis ce matin (TP 1) mais il vit dans un projet isolé. Sofia Marques veut, avant la fin
de la journée, **la solution complète de Trame 2** telle qu'elle sera versionnée dans Azure Repos : un projet par
couche, des conventions appliquées à la compilation, et un premier flux de bout en bout qui prouve que
l'architecture tient. Julien Delcourt a tranché dans son ADR : le flux de commande sera un pipeline de messages ;
le transport (Azure Service Bus) viendra plus tard, il ne doit **rien changer** au code des handlers.

Votre binôme livre ce squelette et le bus in-process qui va avec. Marc Vandewalle sera le premier utilisateur :
il veut voir un ordre de préparation par entrepôt, au format de son WMS, et savoir ce qui est parti en dead
letter et pourquoi.

## Point de départ

Le TP est autonome : vous partez d'un dossier vide. Si vous avez le projet `Textinord.Trame.Domain` du TP 1,
vous pouvez le réutiliser à l'étape 2 ; sinon le TP fournit le minimum nécessaire.

Prérequis vérifiés :

```bash
dotnet --version        # 10.0.x
dotnet nuget list source # au moins nuget.org
```

## Étapes

### Étape 1 — Le squelette de solution (15 min)

Créez la solution et les sept projets. Nom de la solution : `Textinord.Trame`. Nom des projets :
`Textinord.Trame.Domain`, `.Application`, `.Infrastructure` (bibliothèques), `.Api` (`webapi`), `.Worker`
(`worker`), et dans `tests/` : `Textinord.Trame.Domain.Tests`, `Textinord.Trame.Application.Tests` (`xunit`).

```bash
mkdir Textinord.Trame && cd Textinord.Trame
dotnet new sln -n Textinord.Trame --format sln
dotnet new classlib -n Textinord.Trame.Domain -o src/Textinord.Trame.Domain
dotnet new classlib -n Textinord.Trame.Application -o src/Textinord.Trame.Application
dotnet new classlib -n Textinord.Trame.Infrastructure -o src/Textinord.Trame.Infrastructure
dotnet new webapi -n Textinord.Trame.Api -o src/Textinord.Trame.Api
dotnet new worker -n Textinord.Trame.Worker -o src/Textinord.Trame.Worker
dotnet new xunit -n Textinord.Trame.Domain.Tests -o tests/Textinord.Trame.Domain.Tests
dotnet new xunit -n Textinord.Trame.Application.Tests -o tests/Textinord.Trame.Application.Tests
dotnet sln add src/*/*.csproj tests/*/*.csproj
```

Puis :

1. Ajoutez les références de projet dans le sens autorisé (slide 32) : `Application → Domain`,
   `Infrastructure → Application`, `Api → Application + Infrastructure`, `Worker → Application + Infrastructure`,
   chaque projet de tests → le projet qu'il teste.
2. Créez `Directory.Build.props` à la racine avec : `TargetFramework` net10.0, `Nullable` et `ImplicitUsings`
   activés, `TreatWarningsAsErrors`, `EnableNETAnalyzers`, `AnalysisLevel` `latest-recommended`,
   `EnforceCodeStyleInBuild`, `RootNamespace` et `AssemblyName` égaux au nom du projet ; et une section
   conditionnelle `$(MSBuildProjectName.EndsWith('.Tests'))` qui ajoute les quatre packages xUnit et
   `<Using Include="Xunit" />`.
3. Créez `Directory.Packages.props` (`ManagePackageVersionsCentrally`) avec les versions de tous les packages ;
   retirez ensuite les attributs `Version` des `.csproj`. Supprimez `Class1.cs` partout.
4. Créez `.editorconfig` : namespaces file-scoped, `var`, et **au minimum** trois règles de nommage en sévérité
   `warning` : préfixe `I` pour les interfaces, suffixe `Async` pour les méthodes `async`, `_camelCase` pour les
   champs privés, **plus la ligne `dotnet_diagnostic.IDE1006.severity = warning`** (sans elle, `dotnet build`
   ignore la sévérité des règles de nommage). Ajoutez une section `[tests/**.cs]` qui désactive la règle du
   suffixe `Async`.
5. Créez `global.json` (bande `10.0.100`, `rollForward` `latestFeature`) et un `.gitignore` (`bin/`, `obj/`).

**Point de contrôle 1** : `dotnet build` réussit sans avertissement. Ajoutez temporairement
`public interface messageBus { }` dans `Application` : la compilation doit échouer (IDE1006). Retirez-la.

### Étape 2 — Le domaine minimal (10 min)

Dans `Domain`, si vous n'avez pas le TP 1 : `StatutCommande` (énumération du fil rouge), `NumeroCommande`
(record struct validant `CMD-AAAA-NNNNNN`), `LigneCommande` (référence, quantité, prix, remise, code entrepôt),
`Commande` (numéro, client, pays de livraison, urgente, lignes, statut, `Valider(IDisponibiliteStock)`,
`Annuler()`), `Entrepot` (record avec `Roubaix` = `RBX`, `Lesquin` = `LSQ`, `Tous`, `ParCode`).

Deux tests dans `Domain.Tests` suffisent pour ce TP : validation réussie quand le stock est disponible,
annulation refusée après le début de la préparation.

**Point de contrôle 2** : `dotnet test tests/Textinord.Trame.Domain.Tests` : 2 tests verts.

### Étape 3 — Le bus in-process (20 min)

Dans `Application/Messaging`, implémentez :

- `MessageEnvelope` : `MessageId`, `CorrelationId`, `CausationId`, `Type` (nom du type de corps + `.v1`),
  `Corps` (object), `EmisLe`, `ExpireLe`, `NumeroSequence` / `TailleSequence` ; une fabrique `Creer<T>(corps,
  emisLe, correlationId?, dureeDeVie?)` et une méthode `Deriver<T>(corps, emisLe)` qui conserve la corrélation et
  renseigne la causation ; `EstExpire(maintenant)` ; `CorpsEnTantQue<T>()`.
- `MessageChannel` : un `Channel<MessageEnvelope>` **borné** (`FullMode = Wait`, `SingleReader = true`), nommé.
- `MessageHandler` (délégué) et `IMessageContext` : `Message`, `Canal`, `Maintenant`, `PublierAsync<T>` (dérivé),
  `PublierEnveloppeAsync` (tel quel), `RejeterAsync(raison)`.
- `DeadLetterChannel` : dépôt `(message, canal, raison, date)`, liste consultable, compteur.
- `IMessageBus` et `InProcessMessageBus` : `DeclarerCanal`, `Abonner(canal, handler)`, `PublierAsync<T>`,
  `PublierEnveloppeAsync`, `DemarrerAsync` (une boucle `ReadAllAsync` par canal, dispatch séquentiel aux
  handlers), `ArreterAsync` (fermer les canaux **dans l'ordre de déclaration** et attendre chaque boucle).
  Règles : message expiré → dead letter ; exception d'un handler → dead letter, la boucle continue ; canal
  sans abonné → dead letter ; canal inconnu à la publication → exception. Le bus reçoit un `TimeProvider`
  (injectable) pour dater et tester l'expiration.

**Point de contrôle 3** : un test `Application.Tests` publie deux messages sur un canal dont le handler lève sur
le premier ; à l'arrêt du bus, le second a été traité et la dead letter contient le premier avec la raison.

### Étape 4 — Routeur, traducteur, pipeline (15 min)

- `ContentBasedRouter` : règles ordonnées `Quand<T>(nom, condition, canalCible)`, `Rejeter<T>(nom, condition)`,
  `Sinon(canal)`, une méthode `Decider(message)` testable sans bus, et `RouterAsync` utilisable comme handler
  (publie **la même enveloppe** sur le canal choisi, ou rejette avec le nom de la règle).
- Messages (`Application/Commandes/Messages`) : `CommandeValidee` (numéro, client, pays nullable, urgente,
  lignes avec code entrepôt), `OrdrePreparationEntrepot` (format WMS : `NumeroOrdre` = `OP-AAAA-NNNNNN-RBX`,
  `Site` en majuscules, `TypeFlux` `FRANCE` ou `EXPORT`, `Priorite` `NORMALE` ou `URGENTE`, lignes numérotées),
  `CommandeParEntrepot`.
- `IMessageTranslator<TSource, TCible>` et `OrdrePreparationTranslator` : pur, lève `TraductionException` si
  l'entrepôt est inconnu ou si aucune ligne ne le concerne.
- `SplitterParEntrepot` : un ordre par entrepôt concerné, publié sur `entrepot.<CODE>` avec `AvecSequence(i, n)`.
- `PipelineCommandes` : déclare les canaux amont → aval (`commandes.validees`, `preparations.france`,
  `preparations.export`, `entrepot.RBX`, `entrepot.LSQ`, `audit`), abonne le routeur (`FR` → france, sinon
  export, pays absent → rejet), le splitter sur les deux canaux `preparations.*`, et un `IEntrepotGateway`
  (interface dans `Application`, implémentation journalisée dans `Infrastructure`) sur les canaux `entrepot.*`.

**Point de contrôle 4** : le test de bout en bout — une commande France sur RBX et LSQ produit deux ordres
(séquence 1/2 et 2/2), avec la `CorrelationId` de l'événement et une `CausationId` égale à son `MessageId` ;
zéro dead letter.

### Étape 5 — Tests et hôtes (15 min)

Complétez pour atteindre **au moins huit tests** dans `Application.Tests` : routeur France, routeur export,
rejet pays absent, traducteur RBX, traducteur entrepôt inconnu, expiration (avec un `TimeProvider` fixe que
vous avancez de six minutes), handler en échec, bout en bout. Puis :

- `Infrastructure` : `AddTrameInfrastructure()` (dépôt de commandes en mémoire pré-rempli avec trois commandes
  du fil rouge, stock en mémoire, passerelle entrepôt qui journalise via `ILogger`, `IHostedService` qui
  démarre et arrête le bus).
- `Application` : `AddTrameApplication()` (TimeProvider, dead letter, bus, traducteur, pipeline, cas d'usage
  `ValiderCommandeHandler` qui charge, valide, persiste, publie `CommandeValidee` avec une durée de vie de 4 h).
- `Worker` : un `BackgroundService` qui valide les trois commandes, publie un message sans pays, affiche le
  bilan (ordres transmis, dead letters) et arrête l'hôte.
- `Api` : `POST /commandes/{numero}/validation`, `GET /dead-letters`, `GET /ordres`, OpenAPI activé.

**Point de contrôle 5** : `dotnet test` (solution entière) : tous verts ; `dotnet run --project
src/Textinord.Trame.Worker` affiche trois validations, trois ou quatre ordres, une dead letter « pays de
livraison absent ».

## Livrable

Le dossier `Textinord.Trame/` complet (sans `bin/` ni `obj/`), avec :

- `Textinord.Trame.sln`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `global.json`,
  `.gitignore` ;
- `src/` (cinq projets) et `tests/` (deux projets, au moins dix tests au total) ;
- un fichier `docs/architecture.md` de dix lignes : les projets, les références autorisées, le flux du pipeline.

## Dépannage

| Symptôme | Cause probable | Correction |
|---|---|---|
| `NU1903` : package vulnérable, la compilation échoue | version transitive de `Microsoft.OpenApi` signalée ; `TreatWarningsAsErrors` en fait une erreur | montez `Microsoft.AspNetCore.OpenApi` à la dernière 10.0.x dans `Directory.Packages.props` |
| `CA1848` sur chaque `logger.LogInformation(...)` | vous avez activé la règle dans `.editorconfig` | utilisez `[LoggerMessage]` sur une méthode `partial`, ou passez CA1848 en `suggestion` |
| `CA1873` : évaluation d'argument coûteuse dans un appel de log | expression (`string.Join`, `ToString()`) passée directement | calculez la valeur dans une variable locale avant l'appel |
| `CS0121` : appel ambigu entre `PublierAsync<T>` et la surcharge enveloppe | un `MessageEnvelope` satisfait aussi le générique | nommez la surcharge `PublierEnveloppeAsync` |
| `dotnet test` ne se termine jamais | `ArreterAsync` appelé avant `DemarrerAsync`, ou un canal jamais fermé | démarrez avant d'arrêter ; fermez les canaux dans l'ordre de déclaration |
| `CanalInconnuException` à la publication | canal publié avant d'être déclaré, ou faute de frappe dans le nom | centralisez les noms dans une classe `CanauxTrame` de constantes |
| IDE1006 sur un test `async` | règle du suffixe `Async` appliquée aux tests | section `[tests/**.cs]` avec `dotnet_naming_rule.<règle>.severity = none` |
| `interface messageBus` compile quand même (ou seule CA1715 se déclenche) | la sévérité écrite dans `dotnet_naming_rule.*.severity` n'est lue que par l'IDE | ajoutez `dotnet_diagnostic.IDE1006.severity = warning` dans la section `[*.cs]` |

## Grille d'évaluation

| Critère | Points |
|---|---|
| Solution complète, sept projets, références dans le bon sens, compile sans avertissement | 3 |
| `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig` avec règles de nommage actives (une violation volontaire fait échouer le build) | 3 |
| Bus in-process : canaux bornés, boucles par canal, expiration et échec vers la dead letter, arrêt propre | 4 |
| Routeur par contenu testable sans bus ; traducteur pur avec exception explicite ; corrélation et causation propagées ; séquence 1/n | 4 |
| Tests : au moins huit dans `Application.Tests`, dont un de bout en bout, tous verts | 3 |
| Worker ou API fonctionnel, `docs/architecture.md` à jour | 2 |
| Nommage et lisibilité (français cohérent, pas d'abréviation, pas de code mort) | 1 |
| **Total** | **20** |

## Teardown

```bash
dotnet clean
find . -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
```

Conservez le dossier : le TP 4 (persistance) remplace le dépôt en mémoire par EF Core, et le TP 6 branche
MassTransit à la place du bus in-process.
