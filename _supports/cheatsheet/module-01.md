# Cheatsheet — Module 1 : Architecturer un SI .NET, styles, couches et design patterns

Condensé apprenant. Fil rouge : Textinord, Trame (WinForms + WCF + MSMQ + SQL Server) → Trame 2 (.NET 10, ASP.NET Core, Blazor, MAUI, EF Core, Azure).

## Définitions en une ligne

| Terme | Définition |
|---|---|
| Architecture | L'ensemble des décisions qui coûtent cher à changer ; son but est d'aplatir le coût du changement dans la durée |
| SI d'entreprise | Applications, données, flux et acteurs qui font tourner l'entreprise ; se cartographie avant de se transformer |
| Abstraction | Cacher un choix derrière un contrat (interface, schéma d'API, format de message) pour pouvoir le changer |
| Couche | Regroupement technique : même rôle, mêmes règles de dépendance (Domain, Application, Infrastructure, Api) |
| Module | Regroupement fonctionnel : un sous-domaine, ses règles, ses données (Commandes, Catalogue, Préparation) |
| Composant | Unité déployable ou remplaçable avec un contrat (un assembly, un conteneur) |
| Service | Composant exposé à distance derrière un contrat réseau |
| Fournisseur / consommateur | Qui publie le contrat / qui en dépend ; le contrat se conçoit avec le consommateur |
| ADR | Architecture Decision Record : contexte, options, décision, conséquences — une page, numérotée, immuable |
| IoC / DI | La classe reçoit ses dépendances au lieu de les créer ; le conteneur les fabrique et gère leur durée de vie |
| Bootstrapper | `Program.cs` (Generic Host, `WebApplicationBuilder`) : le seul endroit qui connaît toutes les implémentations |
| Stateless | Aucune session en mémoire entre deux requêtes ; l'état vit dans Redis, SQL ou le jeton |
| CQRS | Modèle d'écriture (règles) séparé du modèle de lecture (projections) ; sans event sourcing par défaut |
| Strangler Fig | Migration progressive : l'ancien reste en production, un proxy (YARP) route chaque fonctionnalité migrée vers le nouveau |
| ACL | Anti-Corruption Layer : traduit le modèle de l'ancien système vers celui du nouveau |

## Les critères d'une bonne architecture (ISO 25010, à classer)

Maintenabilité · Performance · Sécurité · Fiabilité · Évolutivité · Observabilité · Coût · Réversibilité.
Une architecture est bonne **pour des critères explicites et classés**, jamais dans l'absolu.

## Styles d'architecture : quand

| Style | Apporte | Coûte | Textinord |
|---|---|---|---|
| 2-tiers | simplicité | logique dupliquée, pas de test | jamais en neuf |
| 3-tiers / n-tiers | règles centralisées | un déploiement | base de Trame 2 |
| SOA / ESB / web services | contrats, réutilisation | bus central lourd | intégration ERP, EDI |
| WOA / REST | HTTP, JSON, OpenAPI | pas de transaction distribuée | API Trame 2 |
| Micro-services | autonomie par service | exploitation, données réparties | pas avec 6 développeurs |
| Monolithe modulaire | modules étanches, un déploiement | discipline sur les références | **choix de Trame 2** |
| Event-driven | découplage temporel, résilience | cohérence à terme | ordres, notifications |
| Clean / onion / hexagonale | domaine au centre, ports | plus de types | organisation interne |

Règle de dépendance : tout pointe vers `Domain` ; `Domain` ne référence aucun paquet technique.

## Les couches et leur contenu

- **Accès aux données** : repositories, `DbContext`, requêtes, mapping.
- **Métier** : entités (règles + état), services métier (règles sans entité), agents (adaptateurs vers l'extérieur), workflows (machines à états, orchestrations).
- **Présentation** : API, Blazor, MAUI, worker — entrée et sortie.
- **Transverses** : logging (`ILogger<T>`), cache (`IDistributedCache`), configuration (`IOptions<T>`), sécurité, résilience (Polly) — injectées, jamais héritées.

## SOLID en une phrase chacun

| Lettre | Principe | Test rapide |
|---|---|---|
| S | Single Responsibility : une classe, une raison de changer | Combien de raisons de changer ? Plus d'une : découper |
| O | Open / Closed : étendre sans modifier | Ajouter un cas = ajouter une classe, pas un `case` |
| L | Liskov Substitution : la sous-classe honore le contrat | Une méthode héritée qui lève `NotSupportedException` viole L |
| I | Interface Segregation : interfaces petites, côté consommateur | Un mock de test doit-il fournir dix méthodes pour en tester une ? |
| D | Dependency Inversion : dépendre d'abstractions que le métier possède | `new SqlConnection` dans le métier viole D |

## Design patterns en une phrase

**Création** — Factory Method : déléguer la création à une méthode · Abstract Factory : une famille d'objets cohérents · Builder : construire pas à pas (`WebApplicationBuilder`) · Singleton : `AddSingleton<T>()`, plus de `static Instance`.

**Structure** — Adapter : rendre compatible une interface existante · Decorator : ajouter un comportement autour d'un contrat (`DelegatingHandler`) · Facade : une entrée simple devant un sous-système · Composite : traiter un groupe comme un élément · Proxy : contrôler l'accès (client HTTP typé, lazy loading).

**Comportement** — Strategy : interchanger un algorithme (`IPolitiqueRemise`) · Observer : notifier sans connaître les abonnés (`event`, événements de domaine) · Command : réifier une action · Template Method : squelette fixe, étapes redéfinies · Chain of Responsibility : pipeline de middlewares · Mediator : un point de rendez-vous (MediatR, Wolverine) · State : comportement selon l'état (table de transitions).

**Avancés** — Specification : règle nommée et composable (`Et`, `Ou`, `Non`) · Pipeline / Middleware : chaque maillon fait une chose · Options : `IOptions<T>` validé au démarrage · Result : l'échec métier est une valeur, pas une exception · Null Object : `SansRemise.Instance` plutôt que `null`.

## Patterns de Fowler (PoEAA) et leur incarnation .NET

| Famille | Pattern | Aujourd'hui |
|---|---|---|
| Sources de données | Table Data Gateway · Row Data Gateway · Active Record · Data Mapper · Repository · Unit of Work · Query Object | Dapper · `DataRow` · à éviter · **EF Core** · `IDepotCommandes` · `SaveChangesAsync()` · `IQueryable<T>` |
| ORM comportemental | Lazy Load · Identity Map · Unit of Work | proxies (préférer `Include`) · change tracker · `SaveChanges` |
| ORM structurel | Foreign Key Mapping · Association Table Mapping · TPH / TPT / TPC · Embedded Value | navigations + FK · many-to-many · `Use*MappingStrategy()` · owned types, colonnes JSON |
| ORM metadata | Metadata Mapping | conventions, attributs, `IEntityTypeConfiguration<T>` |
| Présentation web | Front Controller · Page Controller · MVC · Template View · Two-Step View · Application Controller | middlewares + routing · Razor Pages · contrôleurs / Minimal APIs · `.cshtml`, `.razor` · `_Layout` · machine à états d'un tunnel |
| Communication | Remote Facade · DTO · Gateway | endpoint grossier · `record` plat · `HttpClient` typé |
| Concurrence déconnectée | Optimistic Offline Lock · Pessimistic Offline Lock · Coarse-Grained Lock | `rowversion` · verrou applicatif avec expiration · version sur l'agrégat |
| États de session | Client · Server · Database | jeton, `localStorage` · à proscrire en scale-out (sinon Redis) · brouillon persisté |
| Autres | Layer Supertype · Registry · Plugin · Service Stub · Money | `EntiteBase` · remplacé par la DI · `AssemblyLoadContext`, keyed services · fausse implémentation pour tests · `decimal` + arrondi, jamais `double` |

## Legacy : ce qu'on ne reproduit pas, et par quoi on remplace

WCF / SOAP → REST + OpenAPI, gRPC · MSMQ → Azure Service Bus, RabbitMQ · Web Forms → Blazor, Razor Pages · WIF → OIDC, Microsoft.Identity.Web, Entra ID · COM+ / DTC → `TransactionScope` local, Outbox, sagas · Linq to SQL → EF Core · BinaryFormatter → System.Text.Json · GAC / strong naming → NuGet, SemVer.

## Squelette de code à retenir

```csharp
// Port possédé par le métier (DIP) ; une implémentation par condition (OCP) ; Null Object (LSP)
public interface IPolitiqueRemise { string ConditionTarifaire { get; } decimal TauxPour(int quantite); }

// Bootstrapper : le seul endroit qui connaît les implémentations
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<IPolitiqueRemise, RemiseCollectivite>();
builder.Services.AddTransient<CalculateurTarif>();   // reçoit IEnumerable<IPolitiqueRemise>

// Result : l'échec métier est une valeur
var passage = commande.Valider();                    // Result<StatutCommande>
if (passage.EstEchec) Console.WriteLine(passage.Erreur!.Code);
```

## Règles Textinord codées au TP 1

Remise client 0-25 % + volume 5 % au-delà de 500 pièces, plafond 30 % · validation seulement si chaque ligne a du stock dans un entrepôt, sinon `EnAttenteStock` · un ordre de préparation par entrepôt à la validation, expédition quand tous sont clos · Brouillon → Validee → EnPreparation → Expediee → Facturee, annulation avant préparation · numéro `CMD-AAAA-NNNNNN` séquentiel par année.
