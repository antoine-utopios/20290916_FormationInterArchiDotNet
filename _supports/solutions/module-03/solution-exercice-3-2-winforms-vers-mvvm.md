# Solution — Exercice 3.2 : d'un formulaire WinForms en code-behind à MVVM

> Document formateur — Ne pas distribuer avant la fin de l'exercice.
> Code complet, compilé et testé (9 tests verts, .NET 10, macOS et Windows) :
> `solutions/module-03/exercice-3-2/` (`Textinord.Saisie.sln`).

## Approche pédagogique

Le code-behind fourni mélange trois responsabilités dans cinq handlers. Le
corrigé les sépare sans changer le comportement observable : mêmes messages,
mêmes règles, même séquence. L'apprenant doit ressentir que **rien n'a été
inventé** : chaque ligne du ViewModel existait déjà dans le Form, au mauvais
endroit. La vue, elle, n'est décrite qu'en prose et en XAML, parce que la
bibliothèque doit compiler sur toutes les machines de la salle.

## Partie 1 — Lecture et classement

| Bloc du Form | Lettre | Destination |
|---|---|---|
| `InitializeComponent`, `MessageBox.Show`, `dgvLignes.DataSource`, `lblTotal.Text`, `btnValider.Enabled` | V | reste dans la vue, remplacé par du binding |
| `FormSaisieCommande_Load` : requête SQL des articles | D | `ICatalogueService.ListerAsync` |
| `btnRechercherClient_Click` : test du code vide, message « Client inconnu », activation du bouton | P | `RechercherClientCommand` + `CanExecute` + `MessageErreur` |
| `btnRechercherClient_Click` : requête SQL du client | D | `IClientService.TrouverAsync` |
| `btnAjouterLigne_Click` : article choisi, quantité > 0, remise +5 % au-delà de 500, plafond 30, remise à 1 | P | `AjouterLigneCommand`, `Tarification` |
| `btnAjouterLigne_Click` : requête SQL du stock | D | `IStockService.StockDisponibleAsync` |
| `btnRetirerLigne_Click` | P | `RetirerLigneCommand` |
| `btnValider_Click` : procédure stockée + XML | D | `ICommandeService.EnregistrerAsync` |
| `btnValider_Click` : vider la saisie, afficher le numéro | P | `ValiderCommand`, `NumeroCommande` |

Règle métier reconnue : remise client (0 à 25 %) + 5 % strictement au-delà de
500 pièces, plafond 30 % — celle du fil rouge, qui vit dans
`Textinord.Trame.Domain` depuis le TP 1. Ici on la garde dans une classe
`Tarification` de la bibliothèque pour que l'exercice reste autonome.

## Partie 2 — Les contrats

```csharp
public sealed record Article(string Reference, string Libelle, decimal PrixBase);
public sealed record Client(string Code, string RaisonSociale, decimal RemiseClientPourcent);
public sealed record LigneCommande(Article Article, int Quantite, decimal TauxRemise)
{
    public decimal MontantBrut => Article.PrixBase * Quantite;
    public decimal MontantNet => Math.Round(MontantBrut * (1 - TauxRemise / 100m), 2);
}

public interface IClientService   { Task<Client?> TrouverAsync(string code, CancellationToken ct = default); }
public interface ICatalogueService{ Task<IReadOnlyList<Article>> ListerAsync(CancellationToken ct = default); }
public interface IStockService    { Task<int> StockDisponibleAsync(string reference, CancellationToken ct = default); }
public interface ICommandeService { Task<string> EnregistrerAsync(string codeClient, IReadOnlyList<LigneCommande> lignes, CancellationToken ct = default); }

public static class Tarification
{
    public const int SeuilVolume = 500;
    public const decimal RemiseVolumePourcent = 5m;
    public const decimal PlafondPourcent = 30m;

    public static decimal CalculerTauxRemise(decimal remiseClientPourcent, int quantite)
    {
        var taux = remiseClientPourcent + (quantite > SeuilVolume ? RemiseVolumePourcent : 0m);
        return Math.Min(taux, PlafondPourcent);
    }
}
```

Explication :

- Les interfaces sont **asynchrones** dès le départ : le Form faisait du SQL
  synchrone sur le thread UI, ce qui gelait l'écran à chaque clic. Les
  commandes générées par le toolkit gèrent `Task` nativement.
- `CancellationToken` en dernier paramètre avec valeur par défaut : la
  convention .NET, qui simplifie les substituts NSubstitute (`Arg.Any<CancellationToken>()`).

## Partie 3 — Le ViewModel

Fichier `src/Textinord.Saisie.ViewModels/SaisieCommandeViewModel.cs` :

```csharp
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Textinord.Saisie.ViewModels.Modeles;
using Textinord.Saisie.ViewModels.Services;

namespace Textinord.Saisie.ViewModels;

public sealed partial class SaisieCommandeViewModel : ObservableObject
{
    private readonly IClientService clients;
    private readonly ICatalogueService catalogue;
    private readonly IStockService stocks;
    private readonly ICommandeService commandes;

    public SaisieCommandeViewModel(IClientService clients, ICatalogueService catalogue, IStockService stocks, ICommandeService commandes)
    {
        this.clients = clients;
        this.catalogue = catalogue;
        this.stocks = stocks;
        this.commandes = commandes;

        CodeClient = string.Empty;
        Quantite = 1;
        Lignes.CollectionChanged += LignesModifiees;
    }

    public ObservableCollection<Article> Articles { get; } = [];

    public ObservableCollection<LigneCommande> Lignes { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RechercherClientCommand))]
    public partial string CodeClient { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RaisonSociale))]
    [NotifyCanExecuteChangedFor(nameof(AjouterLigneCommand), nameof(ValiderCommand))]
    public partial Client? ClientCourant { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AjouterLigneCommand))]
    public partial Article? ArticleSelectionne { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AjouterLigneCommand))]
    public partial int Quantite { get; set; }

    [ObservableProperty]
    public partial string? MessageErreur { get; set; }

    [ObservableProperty]
    public partial string? NumeroCommande { get; set; }

    public string RaisonSociale => ClientCourant?.RaisonSociale ?? "Aucun client sélectionné";

    public decimal Total => Lignes.Sum(l => l.MontantNet);

    public async Task ChargerAsync(CancellationToken ct = default)
    {
        Articles.Clear();
        foreach (var article in await catalogue.ListerAsync(ct))
        {
            Articles.Add(article);
        }
    }

    [RelayCommand(CanExecute = nameof(PeutRechercherClient))]
    private async Task RechercherClientAsync()
    {
        MessageErreur = null;
        ClientCourant = await clients.TrouverAsync(CodeClient.Trim());
        if (ClientCourant is null)
        {
            MessageErreur = $"Client {CodeClient.Trim()} inconnu.";
        }
    }

    private bool PeutRechercherClient() => !string.IsNullOrWhiteSpace(CodeClient);

    [RelayCommand(CanExecute = nameof(PeutAjouterLigne))]
    private async Task AjouterLigneAsync()
    {
        MessageErreur = null;
        var article = ArticleSelectionne!;
        var client = ClientCourant!;

        var disponible = await stocks.StockDisponibleAsync(article.Reference);
        if (disponible < Quantite)
        {
            MessageErreur = $"Stock insuffisant pour {article.Reference} : {disponible} disponible(s), {Quantite} demandé(s).";
            return;
        }

        var taux = Tarification.CalculerTauxRemise(client.RemiseClientPourcent, Quantite);
        Lignes.Add(new LigneCommande(article, Quantite, taux));
        Quantite = 1;
    }

    private bool PeutAjouterLigne() => ClientCourant is not null && ArticleSelectionne is not null && Quantite > 0;

    [RelayCommand]
    private void RetirerLigne(LigneCommande ligne) => Lignes.Remove(ligne);

    [RelayCommand(CanExecute = nameof(PeutValider))]
    private async Task ValiderAsync()
    {
        MessageErreur = null;
        NumeroCommande = await commandes.EnregistrerAsync(ClientCourant!.Code, Lignes.ToList());
        Lignes.Clear();
    }

    private bool PeutValider() => ClientCourant is not null && Lignes.Count > 0;

    private void LignesModifiees(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Total));
        ValiderCommand.NotifyCanExecuteChanged();
    }
}
```

Explication, point par point :

- **Propriétés partielles** : `[ObservableProperty] public partial string CodeClient { get; set; }`
  — le générateur (CommunityToolkit.Mvvm 8.4, C# 13+) écrit l'accesseur avec
  `SetProperty`, donc `PropertyChanged`. Plus de champ privé `_codeClient`.
  Un initialiseur `= ""` sur la déclaration est accepté en C# 14 ; le corrigé
  initialise dans le constructeur pour rester lisible sur un projet C# 13.
- **`[NotifyCanExecuteChangedFor]`** remplace les `btnX.Enabled = ...` du Form :
  quand `ClientCourant` change, les commandes Ajouter et Valider réévaluent
  `CanExecute`, et la vue désactive les boutons seule.
- **`[NotifyPropertyChangedFor(nameof(RaisonSociale))]`** remplace
  `lblRaisonSociale.Text = ...` : la propriété calculée notifie quand sa source change.
- **`[RelayCommand]`** sur une méthode `async Task XAsync` produit
  `IAsyncRelayCommand XCommand` ; sur une méthode `void X(T)` produit
  `IRelayCommand<T> XCommand`. `CanExecute = nameof(...)` branche la garde.
- **`MessageErreur`** remplace les cinq `MessageBox.Show` : la vue décide comment
  l'afficher (bandeau, info-bulle, boîte modale). Le ViewModel ne sait pas
  qu'un écran existe.
- **`Lignes.CollectionChanged`** : `Total` est calculé ; sans cet abonnement,
  la vue ne saurait pas qu'il a changé. C'est le seul « câblage » manuel du
  ViewModel, et c'est le piège numéro un de l'exercice.

## Partie 4 — Les tests

Fichier `tests/Textinord.Saisie.ViewModels.Tests/SaisieCommandeViewModelTests.cs`
(9 tests, extraits) :

```csharp
private readonly IClientService clients = Substitute.For<IClientService>();
private readonly ICatalogueService catalogue = Substitute.For<ICatalogueService>();
private readonly IStockService stocks = Substitute.For<IStockService>();
private readonly ICommandeService commandes = Substitute.For<ICommandeService>();

private SaisieCommandeViewModel Creer()
{
    clients.TrouverAsync("HOT-0421", Arg.Any<CancellationToken>()).Returns(HotelBeaulieu);
    clients.TrouverAsync("COL-0007", Arg.Any<CancellationToken>()).Returns(Collectivite);
    catalogue.ListerAsync(Arg.Any<CancellationToken>()).Returns([Gants, Veste]);
    stocks.StockDisponibleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(1000);
    commandes.EnregistrerAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<LigneCommande>>(), Arg.Any<CancellationToken>())
        .Returns("CMD-2026-000042");
    return new SaisieCommandeViewModel(clients, catalogue, stocks, commandes);
}

[Fact]
public async Task La_remise_cumule_client_et_volume_avec_un_plafond_de_30_pourcent()
{
    var vm = Creer();
    var totalNotifie = false;
    vm.PropertyChanged += (_, e) => totalNotifie |= e.PropertyName == nameof(SaisieCommandeViewModel.Total);
    vm.CodeClient = "COL-0007";
    await vm.RechercherClientCommand.ExecuteAsync(null);
    vm.ArticleSelectionne = Gants;
    vm.Quantite = 600;

    await vm.AjouterLigneCommand.ExecuteAsync(null);

    var ligne = Assert.Single(vm.Lignes);
    Assert.Equal(30m, ligne.TauxRemise);
    Assert.Equal(2856m, vm.Total);          // 600 x 6,80 = 4 080 ; - 30 % = 2 856
    Assert.True(totalNotifie);
    Assert.Equal(1, vm.Quantite);
}

[Fact]
public async Task Valider_enregistre_les_lignes_puis_vide_la_saisie()
{
    var vm = Creer();
    vm.CodeClient = "HOT-0421";
    await vm.RechercherClientCommand.ExecuteAsync(null);
    vm.ArticleSelectionne = Veste;
    vm.Quantite = 12;
    await vm.AjouterLigneCommand.ExecuteAsync(null);

    await vm.ValiderCommand.ExecuteAsync(null);

    await commandes.Received(1).EnregistrerAsync(
        "HOT-0421",
        Arg.Is<IReadOnlyList<LigneCommande>>(l => l.Count == 1 && l[0].Quantite == 12 && l[0].TauxRemise == 10m),
        Arg.Any<CancellationToken>());
    Assert.Equal("CMD-2026-000042", vm.NumeroCommande);
    Assert.Empty(vm.Lignes);
    Assert.False(vm.ValiderCommand.CanExecute(null));
}
```

Les neuf tests couvrent : chargement des articles, `CanExecute` de la
recherche, client inconnu, client connu avec `PropertyChanged`, `CanExecute` de
l'ajout (article absent, quantité nulle), stock insuffisant, remise plafonnée,
validation, retrait de ligne.

Vérification :

```bash
cd solutions/module-03/exercice-3-2
dotnet build          # 0 avertissement
dotnet test           # Réussi ! 9 tests
```

## Bonus — Les vues

### WPF (XAML, extrait de `SaisieCommandeWindow.xaml`)

```xml
<Window x:Class="Textinord.Saisie.Wpf.SaisieCommandeWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Saisie de commande" Width="720" Height="560">
  <StackPanel Margin="12">
    <DockPanel>
      <Button DockPanel.Dock="Right" Content="Rechercher" Command="{Binding RechercherClientCommand}" />
      <TextBox Text="{Binding CodeClient, UpdateSourceTrigger=PropertyChanged}" />
    </DockPanel>
    <TextBlock Text="{Binding RaisonSociale}" FontWeight="Bold" Margin="0,4" />
    <TextBlock Text="{Binding MessageErreur}" Foreground="DarkRed" />
    <DockPanel Margin="0,8">
      <Button DockPanel.Dock="Right" Content="Ajouter" Command="{Binding AjouterLigneCommand}" />
      <TextBox DockPanel.Dock="Right" Width="80" Text="{Binding Quantite, UpdateSourceTrigger=PropertyChanged}" />
      <ComboBox ItemsSource="{Binding Articles}" SelectedItem="{Binding ArticleSelectionne}" />
    </DockPanel>
    <DataGrid ItemsSource="{Binding Lignes}" AutoGenerateColumns="True" IsReadOnly="True" Height="220" />
    <TextBlock Text="{Binding Total, StringFormat=Total net : {0:C}}" HorizontalAlignment="Right" />
    <Button Content="Valider la commande" Command="{Binding ValiderCommand}" HorizontalAlignment="Right" />
    <TextBlock Text="{Binding NumeroCommande, StringFormat=Commande {0} créée}" Foreground="DarkGreen" />
  </StackPanel>
</Window>
```

Code-behind : `InitializeComponent(); DataContext = viewModel; Loaded += async (_, _) => await viewModel.ChargerAsync();`
— le ViewModel arrive par injection de dépendances (`services.AddTransient<SaisieCommandeViewModel>()`).

### WinForms (le Form existant, lié au même ViewModel)

```csharp
// Dans FormSaisieCommande, après InitializeComponent() :
var source = new BindingSource { DataSource = viewModel };
txtCodeClient.DataBindings.Add("Text", source, nameof(viewModel.CodeClient), false, DataSourceUpdateMode.OnPropertyChanged);
lblRaisonSociale.DataBindings.Add("Text", source, nameof(viewModel.RaisonSociale));
lblErreur.DataBindings.Add("Text", source, nameof(viewModel.MessageErreur));
cmbArticle.DataSource = viewModel.Articles;
cmbArticle.DataBindings.Add("SelectedItem", source, nameof(viewModel.ArticleSelectionne), false, DataSourceUpdateMode.OnPropertyChanged);
numQuantite.DataBindings.Add("Value", source, nameof(viewModel.Quantite), false, DataSourceUpdateMode.OnPropertyChanged);
dgvLignes.DataSource = viewModel.Lignes;
btnRechercherClient.Click += async (_, _) => await viewModel.RechercherClientCommand.ExecuteAsync(null);
btnAjouterLigne.Click += async (_, _) => await viewModel.AjouterLigneCommand.ExecuteAsync(null);
btnValider.Click += async (_, _) => await viewModel.ValiderCommand.ExecuteAsync(null);
viewModel.AjouterLigneCommand.CanExecuteChanged += (_, _) => btnAjouterLigne.Enabled = viewModel.AjouterLigneCommand.CanExecute(null);
viewModel.ValiderCommand.CanExecuteChanged += (_, _) => btnValider.Enabled = viewModel.ValiderCommand.CanExecute(null);
```

WinForms n'a pas de `Command` : on branche les `Click` sur les commandes et
`CanExecuteChanged` sur `Enabled`. C'est verbeux, mais le Form ne contient
plus une seule règle métier ni une seule requête SQL — c'est tout ce qu'on
demandait pour les 18 mois de coexistence.

## Variantes acceptables

1. Champs privés `[ObservableProperty] private string codeClient;` (syntaxe 8.2) au lieu des propriétés partielles — accepté, signaler que la syntaxe partielle est celle de 2026.
2. `MessageErreur` remplacé par un événement `ErreurSurvenue` ou par `IMessenger` du toolkit — accepté si les tests le couvrent.
3. Substituts écrits à la main (classes `FauxClientService`) au lieu de NSubstitute — accepté, plus verbeux.
4. Une propriété `EstOccupe` gérée par `IAsyncRelayCommand.IsRunning` — accepté, c'est même mieux que la version bonus proposée.

## Erreurs classiques à repérer en correction

| Erreur observée | Cause probable | Comment corriger |
|---|---|---|
| CS9248 « la propriété partielle doit avoir une partie d'implémentation » | CommunityToolkit.Mvvm < 8.4, ou `LangVersion` forcé sous 13 | passer à 8.4.2, `net10.0`, retirer tout `<LangVersion>` |
| `Total` ne bouge pas dans la vue | pas d'abonnement à `CollectionChanged` | ajouter `OnPropertyChanged(nameof(Total))` dans le handler |
| Le bouton Valider reste actif après vidage | `NotifyCanExecuteChanged` oublié | l'appeler dans le handler de collection |
| `MessageBox` encore présent dans le ViewModel | la vue n'a pas été séparée | remplacer par `MessageErreur` ; le ViewModel ne référence aucun `System.Windows.*` |
| Tests qui appellent `vm.AjouterLigneCommand.Execute(null)` sans attendre | commande asynchrone | `await ...ExecuteAsync(null)` |
| `Arg.Any<CancellationToken>()` absent | surcharge avec paramètre par défaut | NSubstitute ne fait pas correspondre les valeurs par défaut : toujours passer l'`Arg` |

## Points à insister en débriefing

- Le ViewModel est **exactement** l'ancien code-behind, réordonné et privé de
  ses `MessageBox` et de ses `SqlConnection` : la migration n'est pas une
  réécriture.
- Ce ViewModel se lie **aujourd'hui** au WinForms migré sur .NET 10, **demain**
  à WPF ou MAUI, et ses règles sont celles que l'extranet Blazor appelle via
  les mêmes interfaces : c'est la réponse au critère « durée de vie » de
  l'exercice 3.1.
- Neuf tests en dix minutes sur une logique qui n'en avait aucun depuis 2011 :
  c'est l'argument que Sofia Marques présentera à Nadia Benali.
