namespace Textinord.Saisie.ViewModels.Services;

public interface IStockService
{
    /// <summary>Stock disponible tous entrepôts confondus pour une référence.</summary>
    Task<int> StockDisponibleAsync(string reference, CancellationToken ct = default);
}
