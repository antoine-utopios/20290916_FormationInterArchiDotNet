using Textinord.Saisie.ViewModels.Modeles;

namespace Textinord.Saisie.ViewModels.Services;

public interface ICommandeService
{
    /// <summary>Enregistre la commande et renvoie son numéro CMD-AAAA-NNNNNN.</summary>
    Task<string> EnregistrerAsync(string codeClient, IReadOnlyList<LigneCommande> lignes, CancellationToken ct = default);
}
