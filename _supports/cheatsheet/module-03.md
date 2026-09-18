# Cheatsheet — Module 3 : applications web et clients (.NET 10, septembre 2026)

Condensé apprenant. Tout ce qui suit est détaillé dans le deck
`slides/module-03-web-et-clients.md`, la démo 3.1 et le TP 3.

## 1. Grille de choix des technologies front

| Technologie | Rendu | Elle convient quand | Elle gêne quand | Pattern | Déploiement |
|---|---|---|---|---|---|
| ASP.NET Core MVC / Razor Pages | HTML côté serveur | contenu, SEO, formulaires, équipe .NET | interactivité riche, hors ligne | MVC / Page Controller | conteneur, App Service, Container Apps |
| Blazor Web App — SSR statique | HTML pur | pages de contenu, listes, POST simples | événements C# (aucun) | composants | idem |
| Blazor — InteractiveServer | HTML puis SignalR | intranet, extranet, équipe C#, accès direct aux services | réseau instable, très grand public | composants (+ MVVM optionnel) | conteneur ; un circuit par onglet |
| Blazor — InteractiveWebAssembly | .NET dans le navigateur | PWA, hors ligne, charge serveur nulle | premier chargement 1 à 3 Mo, SEO | composants | fichiers statiques + API |
| Blazor — InteractiveAuto | Server puis WASM | site grand public : démarrage immédiat puis charge nulle | deux projets, API obligatoire | composants | conteneur + fichiers statiques |
| SPA Angular + API | TypeScript | équipe front dédiée, écosystème JS, plusieurs back-ends | deux pipelines, deux compétences, CORS / BFF | composants + services (NgRx) | CDN, Static Web Apps |
| WinForms sur .NET 10 | Windows natif | maintenance de l'existant, outils internes | multiplateforme, ergonomie | MVP ou MVVM par binding | ClickOnce, MSIX, self-contained |
| WPF sur .NET 10 | Windows natif, XAML | postes métier denses, MVVM, graphiques | multiplateforme, web | MVVM, Dashboard | MSIX, ClickOnce, Intune |
| .NET MAUI | natif Android, iOS, Windows, macOS | terrain, scanners, hors ligne, capteurs | web pur, équipe sans mobile | MVVM + Shell | stores, MDM, APK / AAB |
| WinUI 3 | Windows natif moderne | applications Windows 11 « Fluent » | Windows uniquement | MVVM | MSIX |

**Fin de vie, jamais pour un nouveau développement** : ASP.NET Web Forms
(vers Blazor / MVC), Silverlight (vers Blazor WASM), Xamarin.Forms (vers MAUI),
UWP (vers WinUI 3 / MAUI).

**Sept critères à écrire avant de choisir** : public, réseau, périphériques,
compétences, déploiement, interactivité / hors ligne, durée de vie. La décision
va dans un ADR.

## 2. Patterns de présentation

| | MVC | MVP | MVVM | Dashboard |
|---|---|---|---|---|
| Qui pilote | le Controller, par requête | le Presenter, via `IVue` | le binding (`INotifyPropertyChanged`, `ICommand`) | un conteneur qui compose des blocs autonomes |
| Où vit l'état | nulle part (stateless) | dans le Form, exposé par l'interface | dans le ViewModel | dans chaque bloc |
| Test sans écran | action seule, `WebApplicationFactory` | Presenter + `IVue` simulée | ViewModel seul | un test par bloc |
| Technologies | ASP.NET Core MVC, Razor Pages | WinForms | WPF, MAUI, WinUI 3, Blazor | composants Blazor, `UserControl` WPF, `ContentView` MAUI |

- **ViewModel de page ≠ DTO d'API** : le DTO est le contrat versionné ; le ViewModel est taillé pour un écran. Jamais d'entité EF Core dans une vue.
- **BFF** : une façade par type de client, qui porte l'authentification (cookie côté navigateur) ; le serveur Blazor en est un nativement.

## 3. ASP.NET Core MVC et Razor Pages

Ordre du pipeline dans `Program.cs` (l'ordre des `Use` est l'ordre d'exécution) :

```
UseExceptionHandler → UseHttpsRedirection / UseHsts → MapStaticAssets → UseRouting
→ UseCors → UseAuthentication → UseAuthorization → UseAntiforgery → Map...(endpoints)
```

| Brique | À retenir |
|---|---|
| Routing | attributs `[Route("commandes")]`, `[HttpGet("{numero}")]` ; contraintes `{id:int}` ; conventionnel `{controller=Home}/{action=Index}/{id?}` |
| Model binding | route, query, formulaire, `[FromBody]` JSON → paramètres typés (classe ou `record`) |
| Validation | `DataAnnotations` + `ModelState.IsValid` ; `IValidatableObject` pour les règles croisées ; FluentValidation si ça grossit |
| Filtres | Authorization → Resource → Action → Exception → Result ; `[Authorize]`, `[ServiceFilter<T>]`, globaux via `options.Filters.Add` |
| Antiforgery | tag helper `<form>` + `[ValidateAntiForgeryToken]` (ou `AutoValidateAntiforgeryToken`) ; `UseAntiforgery()` pour Blazor et Minimal APIs |
| Tag helpers, view components, areas | `<input asp-for>`, `<vc:panier-resume />`, `/Adv/Commandes` |
| Razor Pages | `@page` + `PageModel` (`OnGetAsync`, `OnPostAsync`) : Page Controller, une page = un formulaire |
| Post-Redirect-Get | après un POST réussi : `RedirectToAction(...)` |

## 4. SPA Angular + API

- Deux projets, deux outils, un pipeline CI ; `dotnet new angular` n'existe plus (Visual Studio : « Angular and ASP.NET Core », `Microsoft.AspNetCore.SpaProxy`).
- Dev : `proxy.conf.json` redirige `/api` vers `https://localhost:7043`. Prod : même origine derrière YARP / Front Door, sinon **CORS** explicite (`AddCors` + `WithOrigins(...)`, `app.UseCors("front")` après `UseRouting` ; jamais `AllowAnyOrigin` avec des cookies).
- Auth : cookies + BFF (`SameSite=Strict`, antiforgery) par défaut ; jetons OIDC + PKCE (Entra External ID) gardés en mémoire, jamais dans `localStorage`.
- Client généré depuis OpenAPI : `kiota generate -l typescript -d https://localhost:7043/openapi/v1.json -c TrameClient -o src/app/api` (ou NSwag `openapi2tsclient`).
- Déploiement : `ng build` → fichiers statiques (servis par l'API avec fallback `index.html`, Static Web Apps, CDN).

## 5. Blazor Web App

### Render modes

| Mode | Où tourne le composant | Transport | Quand |
|---|---|---|---|
| SSR statique | serveur, à chaque requête | HTML | contenu, SEO, POST simples (`FormName`, `[SupplyParameterFromForm]`) |
| InteractiveServer | serveur, composant en mémoire (circuit) | SignalR | intranet, extranet, accès direct aux services |
| InteractiveWebAssembly | navigateur (runtime .NET WASM) | HTTP / JSON vers l'API | hors ligne, PWA, très grand nombre d'utilisateurs |
| InteractiveAuto | Server la première fois, WASM ensuite | SignalR puis HTTP | grand public |

Déclaration : `@rendermode InteractiveServer` en tête d'un composant, ou
`<Routes @rendermode="InteractiveServer" />` dans `App.razor` (option `-ai`).
Un composant interactif est **pré-rendu** en HTML (donc `OnInitializedAsync`
s'exécute deux fois) sauf `prerender: false` ; `[PersistentState]` (.NET 10)
transporte l'état du pré-rendu vers l'interactif.

### Composants

```razor
[Parameter, EditorRequired] public Article Article { get; set; } = default!;
[Parameter] public EventCallback<AjoutPanier> OnAjouter { get; set; }     // re-rend le parent, gère l'async
[CascadingParameter] public Client? ClientCourant { get; set; }          // fourni par <CascadingValue>
@key="article.Reference"                                                 // stabilité des listes
@inject ICatalogueService Catalogue                                      // DI
```

Cycle de vie : `OnInitialized(Async)` → `OnParametersSet(Async)` → rendu →
`OnAfterRender(Async)(firstRender)` (seul endroit pour l'interop JS) ;
`StateHasChanged()` force un rendu ; `IDisposable` pour se désabonner.

### Formulaires

```razor
<EditForm Model="saisie" OnValidSubmit="ValiderAsync" FormName="commande">
    <DataAnnotationsValidator /> <ValidationSummary />
    <InputText @bind-Value="saisie.ReferenceClient" /> <ValidationMessage For="() => saisie.ReferenceClient" />
    <InputDate @bind-Value="saisie.DateLivraison" /> <InputCheckbox @bind-Value="saisie.AccepteCgv" />
</EditForm>
```

`DataAnnotations` + `IValidatableObject` ; case obligatoire :
`[Range(typeof(bool), "true", "true")]` ; objets imbriqués : `AddValidation()` +
`[ValidatableType]` (.NET 10, expérimental).

### État, durées de vie, interop, authentification

- `AddSingleton` : l'entreprise (catalogue, commandes). `AddScoped` : l'utilisateur, le temps du **circuit** (panier). Deux onglets = deux circuits.
- `ProtectedSessionStorage` côté navigateur ; base ou Redis au-delà d'un circuit.
- Interop : `await JS.InvokeAsync<T>("module.fonction", args)`, `[JSInvokable]` dans l'autre sens.
- Auth : `AddCascadingAuthenticationState()`, `<AuthorizeView>`, `[Authorize]` ; OIDC Entra (module 5).

### Tests bUnit 2.x

```csharp
public class ArticleCarteTests : BunitContext
{
    [Fact] public void Ajouter_remonte_la_quantite()
    {
        AjoutPanier? recu = null;
        var cut = Render<ArticleCarte>(p => p.Add(c => c.Article, article).Add(c => c.OnAjouter, (AjoutPanier a) => recu = a));
        cut.Find("input[type=number]").Change("25");
        cut.Find("button").Click();
        Assert.Equal(25, recu!.Quantite);
    }
}
// pages : Services.AddSingleton<ICatalogueService>(...) dans le constructeur ; cut.WaitForAssertion(() => ...) pour l'asynchrone
```

## 6. Clients Windows et multiplateforme

| | WinForms | WPF | .NET MAUI |
|---|---|---|---|
| Cible | `net10.0-windows` + `<UseWindowsForms>` | `net10.0-windows` + `<UseWPF>` | `net10.0-android`, `-ios`, `-windows`, `-maccatalyst` |
| Vue | designer, code-behind, `DataBindings` / `BindingSource` | XAML, `{Binding}`, `Command`, styles, `DataTemplate` | XAML, `Shell`, handlers (`Handler.Mapper`), `Microsoft.Maui.Essentials` |
| Pattern | MVP ou MVVM par binding | MVVM | MVVM |
| Migration | .NET Upgrade Assistant ; casse : `System.Configuration`, WCF client, `BinaryFormatter`, tiers 32 bits | idem | depuis Xamarin.Forms : Upgrade Assistant |
| Déploiement | ClickOnce, MSIX, self-contained | MSIX, ClickOnce, Intune | Intune / MDM, APK / AAB signé, stores |

### CommunityToolkit.Mvvm (8.4, C# 13+)

```csharp
public sealed partial class SaisieVm(IClientService clients) : ObservableObject
{
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(RechercherCommand))]
    public partial string CodeClient { get; set; } = "";

    [ObservableProperty, NotifyPropertyChangedFor(nameof(RaisonSociale))]
    public partial Client? ClientCourant { get; set; }

    public string RaisonSociale => ClientCourant?.RaisonSociale ?? "Aucun client";

    [RelayCommand(CanExecute = nameof(PeutRechercher))]
    private async Task RechercherAsync() => ClientCourant = await clients.TrouverAsync(CodeClient);
    private bool PeutRechercher() => !string.IsNullOrWhiteSpace(CodeClient);
}
```

Généré : `INotifyPropertyChanged`, `IAsyncRelayCommand RechercherCommand`,
notifications croisées. Une collection calculée (`Total`) exige un abonnement
à `CollectionChanged` + `OnPropertyChanged(nameof(Total))`. Le ViewModel est
une bibliothèque .NET 10 sans référence UI : testée partout avec xUnit + NSubstitute.

## 7. Commandes `dotnet new` et outils

```bash
dotnet new blazor -n App -int Server|WebAssembly|Auto|None [-ai] [-au Individual] [-e]
dotnet new blazorwasm -n App            # WebAssembly autonome (PWA : --pwa)
dotnet new mvc -n App                   # ASP.NET Core MVC
dotnet new razor -n App                 # Razor Pages
dotnet new webapi -n Api                # API (module 5)
dotnet new winforms -n App              # Windows uniquement
dotnet new wpf -n App                   # Windows uniquement
dotnet new maui -n App                  # workload : dotnet workload install maui
dotnet new xunit -n App.Tests && dotnet add App.Tests package bunit
dotnet new sln -n App --format sln && dotnet sln add src/**/*.csproj tests/**/*.csproj
dotnet watch --project src/App          # rechargement à chaud
dotnet dev-certs https --trust          # certificat de développement
upgrade-assistant upgrade Trame.csproj  # migration .NET Framework -> .NET 10
kiota generate -l typescript -d openapi.json -c TrameClient -o src/app/api
```

## 8. Fil rouge : ce que le module a livré

- Extranet clients : Blazor Web App InteractiveServer, catalogue / panier / commande, `ICatalogueService` et `ICommandeService` en mémoire (réimplémentés aux modules 4 et 5).
- Back-office ADV : WinForms migré sur .NET 10 + ViewModels CommunityToolkit.Mvvm testés (exercice 3.2) ; nouveaux écrans en Blazor.
- Entrepôt : .NET MAUI Android, hors ligne, MVVM — même règle de remise, même noyau `Textinord.Trame.Domain`.
