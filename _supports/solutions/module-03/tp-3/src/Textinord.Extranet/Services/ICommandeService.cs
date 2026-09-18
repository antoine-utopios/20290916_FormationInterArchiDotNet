using Textinord.Extranet.Modeles;

namespace Textinord.Extranet.Services;

public interface ICommandeService
{
    Task<Commande> PasserAsync(CommandeSaisie saisie, Client client, IReadOnlyList<LignePanier> lignes, CancellationToken ct = default);

    Task<Commande?> TrouverAsync(string numero, CancellationToken ct = default);
}
