using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Textinord.Trame.Domain;

namespace Textinord.Trame.Infrastructure.Persistence.Configurations;

public sealed class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> builder)
    {
        builder.ToTable("Articles");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Reference).HasMaxLength(30).IsRequired();
        builder.HasIndex(a => a.Reference).IsUnique();

        builder.Property(a => a.Libelle).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Famille).HasMaxLength(50).IsRequired();
        builder.HasIndex(a => a.Famille);

        builder.Property(a => a.PrixBase).HasPrecision(18, 2);

        // Stock par entrepôt : collection possédée (owned), table dédiée, accès par le champ privé.
        builder.OwnsMany(a => a.Stocks, stock =>
        {
            stock.ToTable("StocksArticle");
            stock.WithOwner().HasForeignKey("ArticleId");
            stock.Property<int>("Id");
            stock.HasKey("Id");
            stock.Property(s => s.CodeEntrepot).HasMaxLength(10).IsRequired();
            stock.HasIndex("ArticleId", nameof(StockEntrepot.CodeEntrepot)).IsUnique();
        });
        builder.Navigation(a => a.Stocks).HasField("_stocks");
    }
}
