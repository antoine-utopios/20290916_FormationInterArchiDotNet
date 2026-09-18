using Textinord.Saisie.ViewModels.Modeles;

namespace Textinord.Saisie.ViewModels.Services;

public interface IClientService
{
    Task<Client?> TrouverAsync(string code, CancellationToken ct = default);
}
