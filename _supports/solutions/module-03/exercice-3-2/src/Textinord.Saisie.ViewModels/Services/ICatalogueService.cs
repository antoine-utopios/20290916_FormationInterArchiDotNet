using Textinord.Saisie.ViewModels.Modeles;

namespace Textinord.Saisie.ViewModels.Services;

public interface ICatalogueService
{
    Task<IReadOnlyList<Article>> ListerAsync(CancellationToken ct = default);
}
