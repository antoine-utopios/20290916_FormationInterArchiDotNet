using Textinord.Extranet.Modeles;

namespace Textinord.Extranet.Services;

/// <summary>
/// Panier de l'utilisateur connecté. Enregistré en scoped : dans une Blazor Web App
/// interactive côté serveur, l'instance vit le temps du circuit SignalR.
/// </summary>
public sealed class PanierService
{
    private readonly List<LignePanier> lignes = [];

    public PanierService(Client client)
    {
        Client = client;
    }

    public Client Client { get; }

    public IReadOnlyList<LignePanier> Lignes => lignes;

    public int NombrePieces => lignes.Sum(l => l.Quantite);

    public decimal TotalBrut => lignes.Sum(l => l.MontantBrut);

    public decimal TotalRemise => lignes.Sum(l => l.MontantRemise);

    public decimal TotalNet => lignes.Sum(l => l.MontantNet);

    public bool EstVide => lignes.Count == 0;

    /// <summary>Déclenché à chaque modification : le menu et les pages s'y abonnent.</summary>
    public event Action? Changement;

    public void Ajouter(Article article, int quantite)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quantite, 1);

        var existante = lignes.FirstOrDefault(l => l.Article.Reference == article.Reference);
        if (existante is null)
        {
            lignes.Add(new LignePanier(article, quantite, Client.RemiseClientPourcent));
        }
        else
        {
            existante.Quantite += quantite;
        }

        Changement?.Invoke();
    }

    public void ModifierQuantite(string reference, int quantite)
    {
        var ligne = lignes.FirstOrDefault(l => l.Article.Reference == reference);
        if (ligne is null)
        {
            return;
        }

        if (quantite < 1)
        {
            lignes.Remove(ligne);
        }
        else
        {
            ligne.Quantite = quantite;
        }

        Changement?.Invoke();
    }

    public void Retirer(string reference)
    {
        if (lignes.RemoveAll(l => l.Article.Reference == reference) > 0)
        {
            Changement?.Invoke();
        }
    }

    public void Vider()
    {
        lignes.Clear();
        Changement?.Invoke();
    }
}
