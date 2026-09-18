using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Textinord.Saisie.ViewModels.Modeles;
using Textinord.Saisie.ViewModels.Services;

namespace Textinord.Saisie.ViewModels;

/// <summary>
/// ViewModel de l'écran de saisie de commande de l'ADV. Il ne connaît ni WinForms,
/// ni WPF : la vue se lie à ses propriétés et à ses commandes. Toute la logique qui
/// vivait dans le code-behind de FormSaisieCommande est ici, testable sans écran.
/// </summary>
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

    /// <summary>Appelé par la vue au chargement (Form.Load, Window.Loaded).</summary>
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
