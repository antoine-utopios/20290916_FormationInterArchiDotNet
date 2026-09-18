using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Textinord.Trame.Infrastructure.Persistence;

/// <summary>
/// Fabrique utilisée par « dotnet ef » au moment de la conception (migrations, script).
/// Elle ne sert pas à l'exécution : l'API configure le DbContext via AddTramePersistence.
/// </summary>
public sealed class TrameDbContextFactory : IDesignTimeDbContextFactory<TrameDbContext>
{
    public TrameDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TrameDbContext>()
            .UseSqlite("Data Source=trame2.db")
            // Cible SQL Server / Azure SQL :
            // .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Trame2;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new TrameDbContext(options);
    }
}
