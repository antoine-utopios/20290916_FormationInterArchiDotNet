using Microsoft.EntityFrameworkCore;
using Textinord.Trame.Domain;

namespace Textinord.Trame.Infrastructure.Persistence;

/// <summary>
/// DbContext de Trame 2. Il joue déjà le rôle d'unité de travail (Unit of Work) :
/// le change tracker accumule les modifications, SaveChangesAsync les écrit en une transaction.
/// </summary>
public sealed class TrameDbContext(DbContextOptions<TrameDbContext> options) : DbContext(options), IUniteDeTravail
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Commande> Commandes => Set<Commande>();
    public DbSet<LigneCommande> LignesCommande => Set<LigneCommande>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Une classe de configuration fluent par entité (pattern Metadata Mapping).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrameDbContext).Assembly);
    }

    public Task<int> SauvegarderAsync(CancellationToken ct = default) => SaveChangesAsync(ct);

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        RenouvelerLesJetonsDeVersion();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        RenouvelerLesJetonsDeVersion();
        return base.SaveChanges();
    }

    /// <summary>
    /// Jeton de concurrence géré par l'application (portable SQLite / SQL Server / PostgreSQL).
    /// Toute commande modifiée, directement ou via ses lignes ou ses métadonnées, reçoit
    /// une nouvelle version : l'UPDATE généré contient « WHERE Version = ancienne valeur ».
    /// Sur SQL Server seul, on remplacerait ce mécanisme par une colonne rowversion.
    /// </summary>
    private void RenouvelerLesJetonsDeVersion()
    {
        ChangeTracker.DetectChanges();

        var commandesTouchees = new HashSet<Commande>(ReferenceEqualityComparer.Instance);

        foreach (var entry in ChangeTracker.Entries<Commande>()
                     .Where(e => e.State == EntityState.Modified))
        {
            commandesTouchees.Add(entry.Entity);
        }

        foreach (var entry in ChangeTracker.Entries<LigneCommande>()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var commandeId = entry.Property<int>("CommandeId").CurrentValue;
            var commande = ChangeTracker.Entries<Commande>().FirstOrDefault(c => c.Entity.Id == commandeId)?.Entity;
            if (commande is not null && entry.State != EntityState.Added)
            {
                commandesTouchees.Add(commande);
            }
        }

        foreach (var entry in ChangeTracker.Entries<MetadonneesCommande>()
                     .Where(e => e.State == EntityState.Modified))
        {
            var commande = ChangeTracker.Entries<Commande>()
                .FirstOrDefault(c => ReferenceEquals(c.Entity.Metadonnees, entry.Entity))?.Entity;
            if (commande is not null)
            {
                commandesTouchees.Add(commande);
            }
        }

        foreach (var commande in commandesTouchees)
        {
            var entry = Entry(commande);
            if (entry.State is EntityState.Added or EntityState.Deleted)
            {
                continue;
            }

            entry.Property(c => c.Version).CurrentValue = Guid.NewGuid();
        }
    }
}
