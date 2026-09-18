# Exercice 3.2 — D'un formulaire WinForms en code-behind à MVVM

> Module : 03 — Applications web et clients : ASP.NET Core, Blazor, SPA, MAUI (Jour 2, matin)
> Durée estimée : 30 min
> Difficulté : 3 / 5
> Type : Exercice de refactoring, en binôme, code compilable sur macOS, Linux et Windows

## Objectifs pédagogiques

À la fin de cet exercice, vous serez capable de :

- Repérer, dans un code-behind WinForms, ce qui relève de la vue, de la logique de présentation et de l'accès aux données
- Extraire un ViewModel avec CommunityToolkit.Mvvm (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`, `CanExecute`)
- Rendre ce ViewModel testable avec xUnit et NSubstitute, sans écran ni base de données
- Décrire comment une vue WinForms ou WPF se lie à ce ViewModel

## Prérequis

- Avoir suivi la section 6 du module 3 (WinForms, WPF, CommunityToolkit.Mvvm)
- SDK .NET 10 installé ; accès NuGet pour `CommunityToolkit.Mvvm` 8.4.2, `NSubstitute` 6.2.0, `xunit` 2.9.3
- Le projet WinForms d'origine n'est pas nécessaire : seule la bibliothèque de ViewModels et ses tests compilent (partout)

## Contexte

L'écran de saisie de commande de Trame (`FormSaisieCommande`) est celui que
l'ADV utilise 900 fois par jour en pointe. Sofia Marques veut le porter tel
quel sur .NET 10 dans un premier temps, mais surtout en sortir la logique pour
qu'elle soit testée et réutilisable : les mêmes règles serviront à l'écran
Blazor du back-office et, plus tard, à l'application MAUI. Le code-behind
actuel est reproduit ci-dessous, tel qu'il existe dans Trame (namespace et
contrôles réels, `SqlConnection` en dur, `MessageBox` partout).

### Le code de départ : `FormSaisieCommande.cs` (Trame, .NET Framework 4.8)

```csharp
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Windows.Forms;

namespace Textinord.Trame.Adv
{
    public partial class FormSaisieCommande : Form
    {
        private readonly List<LigneCommande> _lignes = new List<LigneCommande>();
        private decimal _remiseClient;
        private string _codeClient;

        public FormSaisieCommande()
        {
            InitializeComponent();
        }

        private void FormSaisieCommande_Load(object sender, EventArgs e)
        {
            using (var cnx = new SqlConnection(ConfigurationManager.ConnectionStrings["Trame"].ConnectionString))
            using (var cmd = new SqlCommand("SELECT Reference, Libelle, PrixBase FROM Articles WHERE Actif = 1", cnx))
            {
                cnx.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        cmbArticle.Items.Add(new Article(rd.GetString(0), rd.GetString(1), rd.GetDecimal(2)));
                    }
                }
            }
        }

        private void btnRechercherClient_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCodeClient.Text))
            {
                MessageBox.Show("Saisissez un code client.");
                return;
            }

            using (var cnx = new SqlConnection(ConfigurationManager.ConnectionStrings["Trame"].ConnectionString))
            using (var cmd = new SqlCommand("SELECT RaisonSociale, RemisePourcent FROM Clients WHERE Code = @code", cnx))
            {
                cmd.Parameters.AddWithValue("@code", txtCodeClient.Text.Trim());
                cnx.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read())
                    {
                        MessageBox.Show("Client inconnu.");
                        lblRaisonSociale.Text = "";
                        _codeClient = null;
                        btnAjouterLigne.Enabled = false;
                        return;
                    }

                    lblRaisonSociale.Text = rd.GetString(0);
                    _remiseClient = rd.GetDecimal(1);
                    _codeClient = txtCodeClient.Text.Trim();
                    btnAjouterLigne.Enabled = true;
                }
            }
        }

        private void btnAjouterLigne_Click(object sender, EventArgs e)
        {
            var article = cmbArticle.SelectedItem as Article;
            if (article == null)
            {
                MessageBox.Show("Choisissez un article.");
                return;
            }

            var quantite = (int)numQuantite.Value;
            if (quantite <= 0)
            {
                MessageBox.Show("Quantité invalide.");
                return;
            }

            int stock;
            using (var cnx = new SqlConnection(ConfigurationManager.ConnectionStrings["Trame"].ConnectionString))
            using (var cmd = new SqlCommand("SELECT ISNULL(SUM(Quantite), 0) FROM Stocks WHERE Reference = @ref", cnx))
            {
                cmd.Parameters.AddWithValue("@ref", article.Reference);
                cnx.Open();
                stock = (int)cmd.ExecuteScalar();
            }

            if (stock < quantite)
            {
                MessageBox.Show("Stock insuffisant : " + stock + " disponible(s).");
                return;
            }

            var remise = _remiseClient;
            if (quantite > 500)
            {
                remise += 5;
            }
            if (remise > 30)
            {
                remise = 30;
            }

            _lignes.Add(new LigneCommande(article, quantite, remise));
            dgvLignes.DataSource = null;
            dgvLignes.DataSource = _lignes;
            lblTotal.Text = _lignes.Sum(l => l.MontantNet).ToString("C");
            numQuantite.Value = 1;
            btnValider.Enabled = _lignes.Count > 0;
        }

        private void btnRetirerLigne_Click(object sender, EventArgs e)
        {
            if (dgvLignes.CurrentRow == null)
            {
                return;
            }

            _lignes.RemoveAt(dgvLignes.CurrentRow.Index);
            dgvLignes.DataSource = null;
            dgvLignes.DataSource = _lignes;
            lblTotal.Text = _lignes.Sum(l => l.MontantNet).ToString("C");
            btnValider.Enabled = _lignes.Count > 0;
        }

        private void btnValider_Click(object sender, EventArgs e)
        {
            using (var cnx = new SqlConnection(ConfigurationManager.ConnectionStrings["Trame"].ConnectionString))
            using (var cmd = new SqlCommand("dbo.CreerCommande", cnx) { CommandType = System.Data.CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@codeClient", _codeClient);
                cmd.Parameters.AddWithValue("@lignesXml", LignesEnXml(_lignes));
                cnx.Open();
                var numero = (string)cmd.ExecuteScalar();
                MessageBox.Show("Commande " + numero + " créée.");
            }

            _lignes.Clear();
            dgvLignes.DataSource = null;
            lblTotal.Text = "";
            btnValider.Enabled = false;
        }

        private static string LignesEnXml(IEnumerable<LigneCommande> lignes)
        {
            return "<lignes>" + string.Join("", lignes.Select(l =>
                "<l ref=\"" + l.Article.Reference + "\" q=\"" + l.Quantite + "\" r=\"" + l.TauxRemise + "\"/>")) + "</lignes>";
        }
    }

    public class Article
    {
        public Article(string reference, string libelle, decimal prixBase)
        {
            Reference = reference;
            Libelle = libelle;
            PrixBase = prixBase;
        }

        public string Reference { get; }
        public string Libelle { get; }
        public decimal PrixBase { get; }
        public override string ToString() { return Reference + " - " + Libelle; }
    }

    public class LigneCommande
    {
        public LigneCommande(Article article, int quantite, decimal tauxRemise)
        {
            Article = article;
            Quantite = quantite;
            TauxRemise = tauxRemise;
        }

        public Article Article { get; }
        public int Quantite { get; }
        public decimal TauxRemise { get; }
        public decimal MontantNet { get { return Math.Round(Article.PrixBase * Quantite * (1 - TauxRemise / 100m), 2); } }
    }
}
```

## Énoncé

### Partie 1 — Lire et classer (5 min)

Sur le code ci-dessus, marquez chaque bloc avec l'une des trois lettres :
**V** (vue : contrôles, `MessageBox`, `DataSource`), **P** (logique de
présentation : validations, calcul de remise, activation des boutons, état de
la saisie), **D** (accès aux données : SQL, procédure stockée, XML).

Résultat attendu : une liste de ce qui doit sortir du Form (tout ce qui est P
et D) et de ce qui peut y rester (V). Notez la règle métier que vous avez
reconnue : c'est celle du fil rouge (remise client + 5 % au-delà de 500 pièces,
plafond 30 %).

### Partie 2 — Les contrats de services (5 min)

Créez une bibliothèque de classes .NET 10 `Textinord.Saisie.ViewModels`
(`dotnet new classlib`) et ajoutez le package `CommunityToolkit.Mvvm`. Définissez
les interfaces qui remplacent les accès SQL :

- `IClientService.TrouverAsync(string code)` renvoie un `Client?` (code, raison sociale, remise client) ;
- `ICatalogueService.ListerAsync()` renvoie la liste des `Article` ;
- `IStockService.StockDisponibleAsync(string reference)` renvoie un `int` ;
- `ICommandeService.EnregistrerAsync(string codeClient, IReadOnlyList<LigneCommande> lignes)` renvoie le numéro de commande.

Reprenez `Article` et `LigneCommande` en `record`, et isolez la règle de remise
dans une classe statique `Tarification`.

Résultat attendu : la bibliothèque compile, sans aucune référence à
`System.Windows.Forms`, `System.Data.SqlClient` ni `System.Configuration`.

### Partie 3 — Le ViewModel (12 min)

Écrivez `SaisieCommandeViewModel : ObservableObject` avec :

- propriétés observables : `CodeClient`, `ClientCourant`, `ArticleSelectionne`, `Quantite`, `MessageErreur`, `NumeroCommande` ;
- collections : `Articles` et `Lignes` (`ObservableCollection<T>`) ;
- propriétés calculées : `RaisonSociale` (ou « Aucun client sélectionné ») et `Total` ;
- commandes : `RechercherClientCommand`, `AjouterLigneCommand`, `RetirerLigneCommand(LigneCommande)`, `ValiderCommand` ;
- règles de `CanExecute` : rechercher exige un code non vide ; ajouter exige un client trouvé, un article sélectionné et une quantité positive ; valider exige un client et au moins une ligne ;
- une méthode `ChargerAsync()` appelée par la vue au chargement pour remplir `Articles`.

Le comportement doit être identique à celui du Form : client inconnu, stock
insuffisant et quantité nulle produisent un `MessageErreur` (plus de
`MessageBox`), l'ajout d'une ligne remet la quantité à 1, la validation vide
les lignes et expose le numéro de commande.

Résultat attendu : le projet compile ; `Total` change quand `Lignes` change ;
les boutons liés aux commandes se désactivent seuls.

### Partie 4 — Les tests (8 min)

Créez `Textinord.Saisie.ViewModels.Tests` (xUnit + NSubstitute) et écrivez au
moins six tests, dont :

1. un client inconnu produit le message « Client XXX-9999 inconnu. » et empêche l'ajout ;
2. un client connu expose sa raison sociale et lève `PropertyChanged` pour `RaisonSociale` ;
3. le stock insuffisant refuse la ligne avec un message explicite ;
4. 600 gants pour un client à 25 % donnent un taux de 30 % et un `Total` correct ;
5. `ValiderCommand` appelle `ICommandeService.EnregistrerAsync` avec les bonnes lignes puis vide la saisie ;
6. `RetirerLigneCommand` recalcule `Total`.

Résultat attendu : `dotnet test` vert, sur votre poste, sans base de données.

## Indices (à consulter si bloqué)

<details>
<summary>Indice 1 — Propriétés partielles</summary>

Avec CommunityToolkit.Mvvm 8.4 et C# 14, déclarez
`[ObservableProperty] public partial string CodeClient { get; set; }` : le
générateur écrit le reste. `[NotifyCanExecuteChangedFor(nameof(AjouterLigneCommand))]`
sur une propriété rafraîchit l'état du bouton ; `[NotifyPropertyChangedFor(nameof(RaisonSociale))]`
propage vers une propriété calculée.

</details>

<details>
<summary>Indice 2 — Commandes asynchrones</summary>

`[RelayCommand(CanExecute = nameof(PeutAjouterLigne))] private async Task AjouterLigneAsync()`
génère un `IAsyncRelayCommand AjouterLigneCommand` (le suffixe `Async` est
retiré). Dans les tests, appelez `await vm.AjouterLigneCommand.ExecuteAsync(null)`.

</details>

<details>
<summary>Indice 3 — Total et collection</summary>

`Total` dépend de `Lignes` : abonnez-vous à `Lignes.CollectionChanged` dans le
constructeur pour appeler `OnPropertyChanged(nameof(Total))` et
`ValiderCommand.NotifyCanExecuteChanged()`.

</details>

<details>
<summary>Indice 4 — NSubstitute en trois lignes</summary>

```csharp
var clients = Substitute.For<IClientService>();
clients.TrouverAsync("HOT-0421", Arg.Any<CancellationToken>()).Returns(new Client("HOT-0421", "Hôtel Beaulieu Lille", 10m));
await commandes.Received(1).EnregistrerAsync("HOT-0421", Arg.Any<IReadOnlyList<LigneCommande>>(), Arg.Any<CancellationToken>());
```

</details>

## Pour aller plus loin (bonus)

- Décrivez en dix lignes de XAML la fenêtre WPF qui se lie à ce ViewModel
  (`TextBox` + `Binding CodeClient`, `Button` + `Command`, `DataGrid` +
  `ItemsSource`), puis en cinq lignes de C# comment le Form WinForms existant
  se lierait au même ViewModel avec `DataBindings.Add(...)` et un
  `BindingSource`.
- Ajoutez une propriété `EstOccupe` mise à vrai pendant les appels asynchrones,
  et vérifiez par un test qu'elle bloque un double clic sur Valider.
