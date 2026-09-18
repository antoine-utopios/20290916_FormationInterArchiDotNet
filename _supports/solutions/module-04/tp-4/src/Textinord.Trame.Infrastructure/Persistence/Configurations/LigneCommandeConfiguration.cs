using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Textinord.Trame.Domain;

namespace Textinord.Trame.Infrastructure.Persistence.Configurations;

public sealed class LigneCommandeConfiguration : IEntityTypeConfiguration<LigneCommande>
{
    public void Configure(EntityTypeBuilder<LigneCommande> builder)
    {
        builder.ToTable("LignesCommande");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.ReferenceArticle).HasMaxLength(30).IsRequired();
        builder.Property(l => l.PrixUnitaire).HasPrecision(18, 2);
        builder.Property(l => l.RemisePourcent).HasPrecision(5, 2);

        builder.HasOne<Article>()
            .WithMany()
            .HasForeignKey(l => l.ArticleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(l => l.MontantHt);
    }
}
