using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Textinord.Trame.Domain;
using Textinord.Trame.Infrastructure.Lecture;
using Textinord.Trame.Infrastructure.Persistence;
using Textinord.Trame.Infrastructure.Persistence.Repositories;

namespace Textinord.Trame.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Enregistre la persistance de Trame 2. Par défaut : SQLite (fichier local ou connexion fournie).
    /// Cible de production : Azure SQL, voir le bloc commenté.
    /// </summary>
    public static IServiceCollection AddTramePersistence(this IServiceCollection services, string chaineDeConnexion)
    {
        services.AddDbContext<TrameDbContext>(options =>
        {
            options.UseSqlite(chaineDeConnexion);

            // Cible SQL Server / Azure SQL :
            // options.UseSqlServer(chaineDeConnexion, sql =>
            // {
            //     sql.EnableRetryOnFailure(maxRetryCount: 5);   // résilience Azure SQL
            //     sql.CommandTimeout(30);
            // });
        });

        services.AddScoped<IUniteDeTravail>(sp => sp.GetRequiredService<TrameDbContext>());
        services.AddScoped<ICommandeRepository, CommandeRepository>();

        // Dapper partage la connexion du DbContext : même transaction, même durée de vie (scoped).
        services.AddScoped<DbConnection>(sp => sp.GetRequiredService<TrameDbContext>().Database.GetDbConnection());
        services.AddScoped<ICommandeLecture, CommandeLectureDapper>();

        return services;
    }
}
