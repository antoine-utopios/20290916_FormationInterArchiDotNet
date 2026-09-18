# TP 3 — L'extranet Blazor de Textinord : catalogue, panier, commande

> Module : 03 — Applications web et clients : ASP.NET Core, Blazor, SPA, MAUI (Jour 2, matin)
> Durée estimée : 75 min (60 min en séance, le reste en autonomie)
> Difficulté : 3 / 5
> Type : Travaux pratiques guidés, en binôme, évalués sur grille

## Mise en situation

Le site marchand Web Forms de Textinord n'est plus maintenable et n'a jamais
servi aux clients professionnels, qui commandent encore par téléphone ou par
fichier EDI. Trame 2 prévoit un **extranet clients** en Blazor Web App :
consulter le catalogue avec ses conditions tarifaires, remplir un panier,
passer commande. L'API et la base arrivent aux modules 4 et 5 : aujourd'hui,
tout fonctionne en mémoire derrière des interfaces, pour que rien ne change
dans les pages quand la vraie persistance arrivera.

Le client de démonstration est l'**Hôtel Beaulieu Lille** (code `HOT-0421`,
remise client 10 %). L'authentification Entra External ID viendra au module 5.

## Objectifs

- Construire une Blazor Web App .NET 10 interactive côté serveur, structurée en services, composants partagés et pages
- Mettre en œuvre paramètres, `EventCallback`, `EditForm` validé par `DataAnnotations`, service scoped par circuit
- Faire vivre les règles du fil rouge : remise client + volume plafonnée à 30 %, numéro `CMD-AAAA-NNNNNN`, statut `EnAttenteStock`
- Tester des composants et des pages avec bUnit

## Prérequis techniques

### Logiciels à installer

- SDK .NET 10.0.3xx
- Visual Studio 2026 ou VS Code + C# Dev Kit
- Accès NuGet pour `bunit` 2.9.0 (les autres packages viennent du template `xunit`)

### Vérification de l'environnement

```bash
dotnet --version                 # 10.0.3xx
dotnet new list blazor           # le template "Application web Blazor" est présent
dotnet dev-certs https --check   # sinon : dotnet dev-certs https --trust
```

## Architecture cible

```text
 navigateur du client                     serveur Blazor (un circuit SignalR par onglet)
+---------------------+     HTML puis     +------------------------------------------------+
| /catalogue          | <---------------> | Pages : Catalogue, Panier, PasserCommande,     |
| /panier             |     WebSocket     |         Confirmation                           |
| /commande           |                   | Partages : ArticleCarte, FiltreCatalogue,      |
+---------------------+                   |            Pagination, RecapitulatifPanier     |
                                          |            |                                   |
                                          |            v                                   |
                                          | Services : ICatalogueService (singleton)       |
                                          |            PanierService     (scoped)          |
                                          |            ICommandeService  (singleton)       |
                                          |            |                                   |
                                          |            v                                   |
                                          | Modeles  : Article, Client, Tarification,      |
                                          |            LignePanier, CommandeSaisie, Commande|
                                          +------------------------------------------------+
```

## Étapes

### Étape 0 — Le point de départ (5 min)

Objectif : une solution vide qui compile et démarre.

```bash
mkdir textinord-extranet && cd textinord-extranet
dotnet new blazor -n Textinord.Extranet -int Server -ai -o src/Textinord.Extranet
dotnet new xunit  -n Textinord.Extranet.Tests -o tests/Textinord.Extranet.Tests
dotnet add tests/Textinord.Extranet.Tests package bunit
dotnet add tests/Textinord.Extranet.Tests reference src/Textinord.Extranet
dotnet new sln -n Textinord.Extranet --format sln
dotnet sln add src/Textinord.Extranet tests/Textinord.Extranet.Tests
```

Supprimez `Components/Pages/Counter.razor` et `Weather.razor`, puis
`Components/_Imports.razor` reçoit trois lignes supplémentaires :

```razor
@using Textinord.Extranet.Components.Partages
@using Textinord.Extranet.Modeles
@using Textinord.Extranet.Services
```

L'option `-ai` pose `@rendermode="InteractiveServer"` sur `<Routes>` dans
`App.razor` : toutes les pages sont interactives, un seul circuit par onglet,
les services scoped vivent le temps de la session — c'est ce qu'il faut pour un
panier. Dans `Program.cs`, fixez la culture (`CultureInfo.DefaultThreadCurrentCulture`
à `fr-FR`) pour que les prix s'affichent avec une virgule.

Point de contrôle : `dotnet run --project src/Textinord.Extranet` affiche la
page d'accueil ; `dotnet test` exécute le test vide du template.

### Étape 1 — Modèle et catalogue en mémoire (15 min)

Objectif : un service `ICatalogueService` qui filtre et pagine.

1. Dans `Modeles/`, créez les `record` `Article(Reference, Libelle, Famille, PrixBase, StockParEntrepot)`
   avec une propriété calculée `StockTotal`, `Client(Code, RaisonSociale, RemiseClientPourcent)`
   avec un membre statique `Demonstration` (Hôtel Beaulieu Lille, 10 %), la classe
   `FiltreArticles` (`Texte` limité à 40 caractères par `[StringLength]`, `Famille`,
   `EnStockSeulement`) et `PageResultat<T>(Elements, Page, TaillePage, Total)` avec `NombrePages`.
2. Isolez la règle de remise dans `Tarification.CalculerTauxRemise(remiseClient, quantite)` :
   +5 % strictement au-delà de 500 pièces, plafond 30 %, remise client entre 0 et 25 %.
3. Dans `Services/`, écrivez l'interface :

   ```csharp
   public interface ICatalogueService
   {
       Task<IReadOnlyList<string>> ListerFamillesAsync(CancellationToken ct = default);
       Task<PageResultat<Article>> RechercherAsync(FiltreArticles filtre, int page, int taillePage, CancellationToken ct = default);
       Task<Article?> TrouverAsync(string reference, CancellationToken ct = default);
   }
   ```

   et `CatalogueEnMemoire` qui la réalise sur une liste générée : trois familles
   (« Vêtements de travail », « EPI textiles », « Linge hôtellerie »), huit modèles
   par famille, cinq tailles, soit 120 références `VT-001`… `LH-040`, stocks par
   entrepôt `ROU` et `LES` tirés d'un `Random` à graine fixe, quelques ruptures.
4. Enregistrez le service dans `Program.cs` en singleton.

Point de contrôle : un test xUnit `RechercherAsync(new() { Texte = "gant" }, 1, 12)`
renvoie un `Total` > 0 et au plus 12 éléments ; `ListerFamillesAsync` renvoie
trois familles triées.

### Étape 2 — La page Catalogue et ses composants (20 min)

Objectif : liste paginée, filtre validé, ajout au panier remonté par `EventCallback`.

1. `Components/Partages/ArticleCarte.razor` : paramètres `Article` (`[EditorRequired]`)
   et `EventCallback<AjoutPanier> OnAjouter` ; affiche libellé, référence,
   famille, prix, stock (ou « Rupture » avec bouton désactivé), un champ quantité
   et un bouton « Ajouter » qui invoque le callback avec `new AjoutPanier(Article, quantite)`.
2. `Components/Partages/FiltreCatalogue.razor` : un `EditForm` sur un
   `FiltreArticles` privé, `DataAnnotationsValidator`, `InputText`, `InputSelect`
   des familles (paramètre `Familles`), `InputCheckbox` ; `OnValidSubmit` invoque
   `EventCallback<FiltreArticles> OnRechercher` avec une **copie** du filtre.
3. `Components/Partages/Pagination.razor` : paramètres `Page`, `NombrePages`,
   `EventCallback<int> PageChangee` ; boutons Précédent / Suivant désactivés aux
   bornes, fenêtre de cinq numéros au plus.
4. `Components/Pages/Catalogue.razor` (`/catalogue`) : injecte `ICatalogueService`,
   charge les familles et la page 1 dans `OnInitializedAsync`, affiche
   « N article(s), page P sur T », la grille de cartes (`@key`), la pagination,
   et un message de confirmation après un ajout.

Point de contrôle : taper `gant` puis Filtrer réduit la liste sans rechargement
(onglet Réseau : aucune requête HTTP, seul le WebSocket travaille) ; « Suivant »
affiche la page 2 ; taper 41 caractères dans la recherche affiche le message
de validation.

### Étape 3 — Le panier (15 min)

Objectif : un état par utilisateur, des remises justes.

1. `Modeles/LignePanier` : `Article`, `Quantite` modifiable, `PrixUnitaire`,
   `TauxRemise` (via `Tarification`), `MontantBrut`, `MontantRemise`, `MontantNet`.
2. `Services/PanierService` : construit avec un `Client` ; `Lignes`, `NombrePieces`,
   `TotalBrut`, `TotalRemise`, `TotalNet`, `EstVide` ; méthodes `Ajouter`
   (fusionne les lignes de même référence), `ModifierQuantite` (0 retire la
   ligne), `Retirer`, `Vider` ; un événement `Changement` levé à chaque modification.
   Enregistrez-le **scoped** : `builder.Services.AddScoped(_ => new PanierService(Client.Demonstration))`.
3. `Components/Partages/RecapitulatifPanier.razor` : trois paramètres décimaux
   (brut, remises, net) affichés en liste de définitions ; réutilisé par le panier
   et par la commande.
4. `Components/Pages/Panier.razor` (`/panier`) : tableau des lignes avec quantité
   modifiable (`@onchange`), bouton Retirer, récapitulatif, boutons « Vider » et
   « Passer commande ». Dans `NavMenu.razor`, affichez le nombre de pièces à
   côté de « Panier » en vous abonnant à `Changement` (et en vous désabonnant
   dans `Dispose`).
5. Branchez `AjouterAuPanier` de la page Catalogue sur `PanierService.Ajouter`.

Point de contrôle : 100 puis 450 gants `EP-001` donnent **une** ligne de 550
pièces à 15 % de remise (10 % client + 5 % volume) ; passer la quantité à 0
retire la ligne ; ouvrir un second onglet montre un panier **vide** (un
circuit = un panier).

### Étape 4 — La commande (12 min)

Objectif : un formulaire validé et une commande numérotée.

1. `Modeles/CommandeSaisie` avec `DataAnnotations` : `ReferenceClient`
   (`[Required]`, 30 caractères), `AdresseLivraison` (`[Required]`), `CodePostal`
   (`[RegularExpression(@"^\d{5}$")]`), `Ville`, `DateLivraisonSouhaitee`
   (`DateOnly?`, `[Required]`), `Commentaire` (500 caractères), `AccepteCgv`
   (`[Range(typeof(bool), "true", "true")]`). Implémentez `IValidatableObject`
   pour refuser une date avant J+2.
2. `Modeles/Commande` (`record`) et `StatutCommande` (`Brouillon`, `Validee`,
   `EnAttenteStock`, `EnPreparation`, `Expediee`, `Facturee`).
3. `Services/ICommandeService` (`PasserAsync`, `TrouverAsync`) et
   `CommandeEnMemoire` (singleton) : numéro `CMD-AAAA-NNNNNN` séquentiel par
   année (`ConcurrentDictionary<int,int>`), statut `Validee` si chaque ligne a
   un stock suffisant dans **au moins un** entrepôt, sinon `EnAttenteStock` ;
   une commande vide lève `InvalidOperationException`.
4. `Components/Pages/PasserCommande.razor` (`/commande`) : si le panier est
   vide, un avertissement ; sinon un `EditForm` avec `DataAnnotationsValidator`,
   `ValidationSummary`, un `ValidationMessage` par champ, le récapitulatif à
   droite ; `OnValidSubmit` appelle `PasserAsync`, vide le panier et navigue vers
   `/confirmation/{numero}`.
5. `Components/Pages/Confirmation.razor` (`/confirmation/{Numero}`) : rappel du
   numéro, du statut (alerte orange si `EnAttenteStock`), des lignes et du total.

Point de contrôle : soumettre le formulaire vide affiche au moins trois
messages ; un code postal `5900` est refusé ; un formulaire complet mène à
`/confirmation/CMD-2026-000001`, le panier est vide et le badge du menu disparaît.

### Étape 5 — Les tests bUnit (8 min)

Objectif : au moins quatre tests verts, un par famille.

Dans `tests/Textinord.Extranet.Tests/`, une classe par sujet, héritant de
`BunitContext` :

- **composant** : `ArticleCarte` affiche le libellé et le prix ; cliquer sur Ajouter avec la quantité 25 remonte `AjoutPanier(article, 25)` ;
- **page** : `Catalogue` avec un `CatalogueEnMemoire` construit sur une liste de 30 articles de test affiche 12 cartes, puis 3 après le filtre `gant` (`cut.Find("#texte").Change("gant"); cut.Find("form").Submit(); cut.WaitForAssertion(...)`) ;
- **formulaire invalide** : `PasserCommande` avec un panier d'une ligne, soumission vide, présence des messages `.validation-message` et aucune navigation (`NavigationManager.Uri` reste `http://localhost/`) ;
- **service** : `Tarification.CalculerTauxRemise(25, 600)` vaut 30 ; `PanierService.Ajouter` deux fois fusionne les lignes.

Point de contrôle : `dotnet test` → tous verts ; `dotnet build` sans avertissement.

## Livrable attendu

Un dossier (ou dépôt Git) contenant `Textinord.Extranet.sln`, `src/`, `tests/`
et un `.gitignore` excluant `bin/` et `obj/`. `dotnet build` et `dotnet test`
passent sur une machine qui n'a jamais vu le projet. Un fichier `README.md`
de dix lignes indique comment lancer l'application et quel client de
démonstration est connecté.

## Dépannage courant

<details>
<summary>Erreur : le clic sur « Ajouter » ne fait rien</summary>

Cause : la page est rendue en SSR statique, aucun événement C# n'est câblé.
Solution : vérifiez `@rendermode="InteractiveServer"` sur `<Routes>` dans
`App.razor` (option `-ai`), ou ajoutez `@rendermode InteractiveServer` en tête
de la page.

</details>

<details>
<summary>Erreur : le catalogue affiche « Aucun article ne correspond »</summary>

Cause : `CatalogueEnMemoire` a un constructeur qui accepte une collection
d'articles ; enregistré par `AddSingleton<ICatalogueService, CatalogueEnMemoire>()`,
le conteneur a choisi ce constructeur et lui a passé un `IEnumerable<Article>`
vide. Solution : typez ce paramètre `IReadOnlyList<Article>` (que le conteneur
ne sait pas fournir) ou enregistrez avec une fabrique
`AddSingleton<ICatalogueService>(_ => new CatalogueEnMemoire())`.

</details>

<details>
<summary>Erreur : RZ2005 « The 'page' directive must appear at the start of the line »</summary>

Cause : une variable nommée `page` est affichée avec `@page` dans le balisage,
que Razor lit comme la directive. Solution : renommez la variable
(`numeroPage`) ou écrivez `@(page)`.

</details>

<details>
<summary>Erreur : CS0029 « Impossible de convertir Modeles.Commande en Components.Pages.Commande »</summary>

Cause : la page `Commande.razor` et le record `Commande` portent le même nom
dans deux namespaces importés. Solution : nommez la page `PasserCommande.razor`.

</details>

<details>
<summary>Le panier est vide après avoir cliqué sur « Passer commande »</summary>

Cause : la navigation a rechargé la page (lien externe, `href` absolu vers un
autre hôte, ou `forceLoad: true`) et le circuit a été recréé. Solution :
utilisez des liens relatifs (`href="commande"`) ou `NavigationManager.NavigateTo("/commande")`.

</details>

<details>
<summary>Le test bUnit échoue : « Cannot provide a value for property PanierService »</summary>

Cause : le service injecté par la page n'est pas enregistré dans le
conteneur du test. Solution : dans le constructeur de la classe de test,
`Services.AddScoped(_ => new PanierService(Client.Demonstration))` et
`Services.AddSingleton<ICatalogueService>(...)`.

</details>

## Grille d'évaluation

| Critère | Points | Ce qu'on regarde |
|---|---|---|
| Catalogue : liste, filtre validé, pagination | 25 | filtre par texte, famille et stock ; message de validation à 41 caractères ; pagination bornée ; `@key` sur les cartes |
| Panier : service scoped, remises | 20 | fusion des lignes, remise client + volume plafonnée à 30 %, modification et retrait, badge du menu à jour |
| Commande : formulaire, numéro, statut | 20 | `DataAnnotations` + `IValidatableObject`, `CMD-AAAA-NNNNNN` séquentiel, `EnAttenteStock` si stock insuffisant, redirection vers la confirmation |
| Tests bUnit et xUnit | 20 | au moins quatre tests verts couvrant composant, page, formulaire invalide et service ; `WaitForAssertion` pour l'asynchrone |
| Qualité et structure | 15 | services derrière des interfaces, composants réutilisables, aucune logique métier dans le balisage, `dotnet build` sans avertissement, `.gitignore` |

Seuil : 50 points ; l'évaluation reste formative, le débriefing compte plus que la note.

## Teardown

```bash
dotnet clean
find . -type d \( -name bin -o -name obj \) -exec rm -rf {} +
```

Gardez le dossier : le module 4 remplace `CatalogueEnMemoire` par une
implémentation EF Core, le module 5 par un client HTTP de l'API Trame 2.

## Pour aller plus loin

- Ajoutez une page `/commandes` listant les commandes passées dans la session, en SSR statique avec `@rendermode` retiré sur cette page seulement : observez ce qui change pour le panier.
- Persistez le panier avec `ProtectedSessionStorage` pour survivre à un rechargement de page, puis discutez de ce que cela impliquerait pour un déploiement sur deux instances Container Apps.
- Ajoutez un `[PersistentState]` (.NET 10) sur `resultat` dans la page Catalogue pour éviter le double chargement pré-rendu / interactif.
