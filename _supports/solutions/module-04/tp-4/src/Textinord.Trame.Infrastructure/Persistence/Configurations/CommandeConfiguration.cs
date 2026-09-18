using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Textinord.Trame.Domain;

namespace Textinord.Trame.Infrastructure.Persistence.Configurations;

public sealed class CommandeConfiguration : IEntityTypeConfiguration<Commande>
{
    public void Configure(EntityTypeBuilder<Commande> builder)
    {
        builder.ToTable("Commandes");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Numero).HasMaxLength(15).IsRequired();
        builder.HasIndex(c => c.Numero).IsUnique();

        builder.Property(c => c.Statut).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(c => c.Statut);
        builder.HasIndex(c => c.Date);

        builder.HasOne(c => c.Client)
            .WithMany()
            .HasForeignKey(c => c.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        // Jeton de concurrence optimiste géré par l'application (voir TrameDbContext).
        builder.Property(c => c.Version).IsConcurrencyToken();
        // Variante SQL Server uniquement (colonne rowversion gérée par le moteur) :
        // builder.Property<byte[]>("RowVersion").IsRowVersion();

        // Métadonnées : owned type sérialisé dans une colonne JSON (SQLite, SQL Server, PostgreSQL).
        builder.OwnsOne(c => c.Metadonnees, meta =>
        {
            meta.ToJson("Metadonnees");
        });
        builder.Navigation(c => c.Metadonnees).IsRequired();

        // Les lignes appartiennent à l'agrégat : chargées par le champ privé, supprimées avec la commande.
        builder.HasMany(c => c.Lignes)
            .WithOne()
            .HasForeignKey("CommandeId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Lignes).HasField("_lignes");

        builder.Ignore(c => c.TotalHt);
    }
}
