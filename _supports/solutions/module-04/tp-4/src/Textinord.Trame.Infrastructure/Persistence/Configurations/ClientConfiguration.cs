using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Textinord.Trame.Domain;

namespace Textinord.Trame.Infrastructure.Persistence.Configurations;

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Code).HasMaxLength(20).IsRequired();
        builder.HasIndex(c => c.Code).IsUnique();

        builder.Property(c => c.RaisonSociale).HasMaxLength(200).IsRequired();

        // Enum stockée sous forme de texte : lisible dans la base et dans les requêtes Dapper.
        builder.Property(c => c.ConditionTarifaire).HasConversion<string>().HasMaxLength(30);
    }
}
