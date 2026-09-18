# Solution — TP 3 : l'extranet Blazor de Textinord

> Document formateur — Ne pas distribuer avant la fin du TP.
> Code de référence, compilé et testé : `solutions/module-03/tp-3/`
> (`Textinord.Extranet.sln`, .NET 10, bUnit 2.9.0 — `dotnet build` sans
> avertissement, `dotnet test` : 27 tests verts).

## Approche pédagogique

Le TP assemble tout ce que la démo 3.1 a montré, plus deux briques d'état
(panier scoped, carnet de commandes singleton) et un formulaire complet. La
correction se fait étape par étape, en validant chaque point de contrôle sur
la machine d'un binôme. Le code de référence est la source de vérité ; ce
document explique les **choix** qui ne se voient pas dans le code, et ce qui
est acceptable autrement.

## Structure de la solution

```
solutions/module-03/tp-3/
├── Textinord.Extranet.sln
├── .gitignore
├── src/Textinord.Extranet/
│   ├── Program.cs                          culture fr-FR, DI : catalogue et commandes singleton, panier scoped
│   ├── Components/App.razor                <Routes @rendermode="InteractiveServer" />  (option -ai)
│   ├── Components/Layout/NavMenu.razor     badge du panier, abonnement à PanierService.Changement
│   ├── Components/Pages/Home.razor         accueil, conditions tarifaires du client
│   ├── Components/Pages/Catalogue.razor    liste, filtre, pagination, ajout au panier
│   ├── Components/Pages/Panier.razor       lignes modifiables, récapitulatif
│   ├── Components/Pages/PasserCommande.razor  EditForm validé, redirection
│   ├── Components/Pages/Confirmation.razor    numéro, statut, lignes
│   ├── Components/Partages/ArticleCarte.razor, FiltreCatalogue.razor, Pagination.razor, RecapitulatifPanier.razor
│   ├── Modeles/Article.cs, Client.cs, Tarification.cs, FiltreArticles.cs, PageResultat.cs, LignePanier.cs, CommandeSaisie.cs, Commande.cs
│   └── Services/ICatalogueService.cs, CatalogueEnMemoire.cs, CatalogueDeDemonstration.cs, PanierService.cs, ICommandeService.cs, CommandeEnMemoire.cs, Format.cs
└── tests/Textinord.Extranet.Tests/
    ├── CatalogueDeTest.cs                  30 articles lisibles (27 vestes, 3 gants dont une rupture)
    ├── Services/TarificationTests.cs, PanierServiceTests.cs, CommandeEnMemoireTests.cs
    ├── Composants/ArticleCarteTests.cs, PaginationTests.cs
    └── Pages/CataloguePageTests.cs, CommandePageTests.cs
```

Exécution :

```bash
cd solutions/module-03/tp-3
dotnet build
dotnet test
dotnet run --project src/Textinord.Extranet     # http://localhost:5xxx/catalogue
```

## Solution détaillée

### Étape 0 — Interactivité globale, et pourquoi

Le TP prend `-ai` : `<Routes @rendermode="InteractiveServer" />` dans
`App.razor`. Raisonnement : l'extranet est interactif de bout en bout, le
panier vit dans un service **scoped**, et en Blazor Server un scope = un
circuit. Avec un seul circuit pour toute la navigation, le panier survit d'une
page à l'autre sans aucun stockage. Le mode par page (démo 3.1) reste juste ;
il oblige à réfléchir à ce qui arrive au panier sur une page SSR — c'est la
question de la section « Pour aller plus loin » du TP.

La culture est fixée dans `Program.cs` (`fr-FR`) et les prix passent par
`Format.Prix(decimal)` (`{0:N2} €`) : les tests comparent donc `"6,80 €"`
quelle que soit la machine — l'espace insécable étroit du format monétaire
ICU (`"C"`) aurait rendu les assertions fragiles.

### Étape 1 — Le catalogue en mémoire et le piège de DI

`CatalogueEnMemoire` a deux constructeurs : sans paramètre (jeu de
démonstration, 120 articles) et avec `IReadOnlyList<Article>` (tests). Le
premier corrigé utilisait `IEnumerable<Article>` et
`AddSingleton<ICatalogueService, CatalogueEnMemoire>()` : le catalogue était
**vide en production**, sans erreur, parce que le conteneur sait toujours
fournir un `IEnumerable<T>` (vide) et choisit le constructeur le plus riche
qu'il peut satisfaire. Deux corrections cumulées dans la solution : le
paramètre est typé `IReadOnlyList<Article>` (non résoluble) et
l'enregistrement passe par une fabrique. Racontez-le : c'est un vrai incident
de production, et il annonce la section DI du module 5.

`RechercherAsync` filtre puis pagine en mémoire ; le contrat (`filtre, page,
taillePage`) est celui qu'une implémentation SQL ou HTTP pourra honorer sans
que la page change.

### Étape 2 — Composants : qui décide

- `ArticleCarte` ne connaît pas le panier : il émet `AjoutPanier(Article, Quantite)`
  par `EventCallback`, puis remet sa quantité à 1. Le parent (`Catalogue`)
  appelle `PanierService.Ajouter`. Testable isolément (`ArticleCarteTests`).
- `FiltreCatalogue` possède son propre `FiltreArticles` et émet une **copie**
  (`Copier()`) : la recherche appliquée ne bouge pas quand l'utilisateur
  retape sans soumettre. Le `FormName` est inutile en interactif mais
  inoffensif ; il rend le composant utilisable en SSR.
- `Pagination` calcule une fenêtre de cinq numéros (`Fenetre()`), désactive
  les bornes et ignore les clics hors plage (`Aller`). Attention à ne pas
  nommer une variable `page` dans le balisage : `@page` est une directive
  (erreur RZ2005 rencontrée en écrivant le corrigé).
- `Catalogue.razor` : `OnInitializedAsync` charge familles + page 1 ;
  `Rechercher` et `ChangerPage` rappellent `ChargerAsync`. Le `@key` sur la
  référence évite de recréer les cartes à chaque page.

### Étape 3 — Le panier scoped

`PanierService` reçoit le `Client` au constructeur
(`AddScoped(_ => new PanierService(Client.Demonstration))`) : au module 5,
la fabrique lira l'identité Entra External ID. `LignePanier` recalcule
`TauxRemise` à chaque lecture depuis `Tarification` : modifier la quantité de
450 à 550 fait passer la remise de 10 % à 15 % sans code supplémentaire.
`MontantRemise` est arrondi à deux décimales (`Math.Round`) — sinon les
totaux affichés ne correspondent pas à la somme des lignes.

`NavMenu` s'abonne à `Changement` dans `OnInitialized` et se désabonne dans
`Dispose` (`@implements IDisposable`). Sans le `Dispose`, chaque circuit
laisserait un abonnement mort ; avec `InvokeAsync(StateHasChanged)`, la mise
à jour arrive sur le bon contexte de synchronisation.

Point à faire constater : un second onglet a un panier vide. Un circuit = un
scope = un panier. Si le métier veut un panier partagé entre onglets, c'est
un stockage serveur par client (module 4) — et un choix, pas un bug.

### Étape 4 — Commande : validation et règles

- `CommandeSaisie` : `DataAnnotations` pour les champs, `IValidatableObject`
  pour « livraison au plus tôt à J+2 » ; `AccepteCgv` avec
  `[Range(typeof(bool), "true", "true")]` est l'idiome standard pour une case
  obligatoire.
- `CommandeEnMemoire` : `ConcurrentDictionary<int,int>` par année pour le
  compteur, `AddOrUpdate` atomique — deux circuits qui commandent en même
  temps ne partagent jamais un numéro. Le statut applique la règle du fil
  rouge : `Validee` si chaque ligne a du stock dans **au moins un** entrepôt
  (`Article.EstDisponible`), sinon `EnAttenteStock`. `TimeProvider` est
  injectable pour les tests qui voudraient figer l'année.
- `PasserCommande.razor` : renommé (et non `Commande.razor`) pour éviter la
  collision avec le record `Commande` — erreur CS0029 rencontrée en écrivant
  le corrigé. `OnValidSubmit` passe la commande, vide le panier puis
  `NavigateTo("/confirmation/{numero}")` ; `enCours` désactive le bouton
  pendant l'appel (double clic).
- `Confirmation.razor` lit `[Parameter] Numero` dans `OnParametersSetAsync`
  (et non `OnInitializedAsync`) pour réagir à un changement de route sans
  recréer le composant.

### Étape 5 — Tests

27 tests, répartis ainsi :

| Classe | Ce qu'elle prouve | Points bUnit |
|---|---|---|
| `TarificationTests` (6) | seuil strict à 500, cumul, plafond, bornes de la remise client | théorie xUnit pure |
| `PanierServiceTests` (3) | fusion des lignes, retrait à quantité 0, événement `Changement`, montant net | xUnit pur |
| `CommandeEnMemoireTests` (3) | format et séquence des numéros, `EnAttenteStock`, commande vide refusée | `GeneratedRegex` |
| `ArticleCarteTests` (3) | rendu, bouton désactivé en rupture, `EventCallback` avec la quantité saisie | `Render<T>(p => p.Add(...))`, `.Change`, `.Click` |
| `PaginationTests` (3) | bornes désactivées, callback Suivant, fenêtre de cinq | `FindAll("button")[^1]` |
| `CataloguePageTests` (5) | 12 cartes, filtre texte, filtre stock, ajout au panier, page 2 | `Services.AddSingleton`, `WaitForAssertion` |
| `CommandePageTests` (4) | panier vide, formulaire vide, formulaire valide + redirection, code postal invalide | `NavigationManager.Uri`, `.validation-message` |

Deux détails qui font gagner du temps aux apprenants :

- `WaitForAssertion` est obligatoire dès qu'un `OnInitializedAsync` ou un
  `OnValidSubmit` est asynchrone, même si le service répond synchronement :
  le rendu suit une boucle de messages.
- La date de livraison du test est calculée (`DateTime.Today.AddDays(7)`) :
  un test qui code `2026-09-30` en dur cassera un jour.

## Variantes acceptables

1. Mode par page (`@rendermode InteractiveServer` sur chaque page) au lieu de `-ai` — accepté si le panier fonctionne d'une page à l'autre ; faire vérifier avec une navigation vers une page SSR.
2. `QuickGrid` (`Microsoft.AspNetCore.Components.QuickGrid`) pour la liste au lieu des cartes — accepté, la pagination intégrée (`PaginationState`) remplace le composant maison ; exiger quand même le filtre validé.
3. Panier stocké dans `ProtectedSessionStorage` — accepté avec la remarque « série sérialisation à chaque changement, et plus de `Changement` synchrone ».
4. Numérotation par `Interlocked.Increment` sur un compteur unique sans année — refuser : la règle du fil rouge dit « séquentiel par année ».
5. Tests bUnit en fichiers `.razor` (`Render(@<ArticleCarte Article="..." />)`) — accepté, exige `Sdk="Microsoft.NET.Sdk.Razor"` sur le projet de tests.

## Erreurs classiques à repérer en correction

| Erreur observée | Cause probable | Comment corriger |
|---|---|---|
| Clic sans effet | page en SSR statique | `-ai` ou `@rendermode` |
| Catalogue vide | constructeur choisi par le conteneur DI | `IReadOnlyList<Article>` + fabrique |
| Remise de 15 % dès 500 pièces | `>=` au lieu de `>` | la règle dit « au-delà de 500 » |
| Total du panier différent de la somme affichée | arrondi absent sur `MontantRemise` | `Math.Round(..., 2)` |
| Deux onglets, un seul panier attendu | confusion scope / utilisateur | expliquer circuit = scope ; renvoyer au module 4 pour un panier persistant |
| Badge du menu figé | pas d'abonnement à `Changement` ou pas d'`InvokeAsync(StateHasChanged)` | voir `NavMenu.razor` |
| `ValidationSummary` vide au premier submit | `DataAnnotationsValidator` manquant dans l'`EditForm` | l'ajouter |
| Test de page qui échoue avec « Cannot provide a value for property » | service non enregistré dans `Services` du test | `Services.AddScoped(...)` dans le constructeur de test |
| Test qui passe seul et échoue en série | `PanierService` partagé entre tests | une instance par classe de test (champ d'instance, pas `static`) |

## Points à insister en débriefing

- Trois durées de vie, trois rôles : singleton pour ce qui appartient à
  l'entreprise (catalogue, commandes), scoped pour ce qui appartient à
  l'utilisateur (panier), transient pour rien ici. Le module 5 formalise.
- Les pages ne contiennent aucune règle : `Tarification`, `PanierService`,
  `CommandeEnMemoire` portent tout, et les tests xUnit purs sont les plus
  rapides à écrire. bUnit vient ensuite, pour le câblage.
- `ICatalogueService` et `ICommandeService` sont les deux interfaces que les
  modules 4 (EF Core, Cosmos DB) et 5 (API, HttpClient) réimplémentent : le
  TP 3 n'est pas jetable, il est le front définitif de Trame 2.
- Le piège du constructeur DI et la collision `Commande` / `Commande` sont
  deux incidents réels rencontrés en écrivant le corrigé : les raconter vaut
  mieux que les cacher.

## Bonus

- Page `/commandes` en SSR statique : retirer l'interactivité sur cette page
  seule impose `@rendermode` par page partout ailleurs (ou `@attribute
  [ExcludeFromInteractiveRouting]` en .NET 9+ sur la page statique) — bon
  moment pour montrer cet attribut.
- `[PersistentState]` (.NET 10) sur `resultat` dans `Catalogue.razor` :
  le pré-rendu et le premier rendu interactif partagent la même page de
  résultats ; observer dans l'onglet Réseau que les données ne sont plus
  chargées deux fois.
