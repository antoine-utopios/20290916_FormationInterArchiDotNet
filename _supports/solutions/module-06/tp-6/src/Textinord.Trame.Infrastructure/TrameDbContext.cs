using Microsoft.EntityFrameworkCore;
using Textinord.Trame.Domain;
using Textinord.Trame.Infrastructure.Outbox;

namespace Textinord.Trame.Infrastructure;

/// <summary>
/// Le DbContext est l'unité de travail : un SaveChangesAsync = une transaction.
/// C'est ce qui permet d'écrire la commande ET son message Outbox atomiquement.
/// </summary>
public sealed class TrameDbContext(DbContextOptions<TrameDbContext> options) : DbContext(options)
{
    public DbSet<Client> Clients => Set<Client>();

    public DbSet<Article> Articles => Set<Article>();

    public DbSet<StockArticle> Stocks => Set<StockArticle>();

    public DbSet<Commande> Commandes => Set<Commande>();

    public DbSet<OrdrePreparation> OrdresPreparation => Set<OrdrePreparation>();

    public DbSet<CompteurCommandes> Compteurs => Set<CompteurCommandes>();

    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>(e =>
        {
            e.HasKey(c => c.Code);
            e.Property(c => c.Code).HasMaxLength(10);
            e.Property(c => c.RaisonSociale).HasMaxLength(120);
            e.Property(c => c.TauxRemise).HasPrecision(5, 4);
        });

        modelBuilder.Entity<Article>(e =>
        {
            e.HasKey(a => a.Reference);
            e.Property(a => a.Reference).HasMaxLength(20);
            e.Property(a => a.Libelle).HasMaxLength(120);
            e.Property(a => a.Famille).HasMaxLength(40);
            e.Property(a => a.PrixBase).HasPrecision(12, 2);
        });

        modelBuilder.Entity<StockArticle>(e =>
        {
            e.ToTable("Stocks");
            e.HasKey(s => new { s.Reference, s.Entrepot });
            e.Property(s => s.Entrepot).HasMaxLength(3);
        });

        modelBuilder.Entity<Commande>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.Numero).IsUnique();
            e.Property(c => c.Numero).HasMaxLength(15);
            e.Property(c => c.CodeClient).HasMaxLength(10);
            e.Property(c => c.Statut).HasConversion<string>().HasMaxLength(20);
            e.HasMany(c => c.Lignes)
                .WithOne()
                .HasForeignKey(l => l.CommandeId)
                .OnDelete(DeleteBehavior.Cascade);
            // La collection est exposée en lecture seule : EF Core écrit dans le champ.
            e.Navigation(c => c.Lignes).HasField("_lignes").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<LigneCommande>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.ReferenceArticle).HasMaxLength(20);
            e.Property(l => l.EntrepotAffecte).HasMaxLength(3);
            e.Property(l => l.PrixUnitaire).HasPrecision(12, 2);
            e.Property(l => l.TauxRemise).HasPrecision(5, 4);
        });

        modelBuilder.Entity<OrdrePreparation>(e =>
        {
            e.ToTable("OrdresPreparation");
            e.HasKey(o => o.Id);
            // Clé d'idempotence : un seul ordre par commande et par entrepôt, quoi qu'il arrive au message.
            e.HasIndex(o => new { o.CommandeId, o.Entrepot }).IsUnique();
            e.Property(o => o.NumeroCommande).HasMaxLength(15);
            e.Property(o => o.Entrepot).HasMaxLength(3);
            e.Property(o => o.Statut).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<CompteurCommandes>(e =>
        {
            e.ToTable("CompteursCommandes");
            e.HasKey(c => c.Annee);
            e.Property(c => c.Annee).ValueGeneratedNever();
        });

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.ToTable("Outbox");
            e.HasKey(m => m.Id);
            e.HasIndex(m => m.EnvoyeLe);
            e.Property(m => m.Type).HasMaxLength(100);
        });
    }
}

/// <summary>Dernier numéro attribué par année : CMD-AAAA-NNNNNN est séquentiel par année.</summary>
public sealed class CompteurCommandes
{
    public int Annee { get; init; }

    public int Dernier { get; set; }
}
