using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Textinord.Trame.Domain.Services;

namespace Textinord.Trame.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Enregistre le DbContext SQLite (chaîne « Trame »), le port stock et l'initialisation de la base.
    /// L'API et le Worker appellent la même méthode : même schéma, même fichier.
    /// </summary>
    public static IServiceCollection AddTrameInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var chaine = configuration.GetConnectionString("Trame") ?? "Data Source=trame2.db";

        services.AddDbContext<TrameDbContext>(options => options.UseSqlite(chaine));
        services.AddScoped<IStockDisponible, StockParTable>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService<InitialisationBase>();

        return services;
    }
}
