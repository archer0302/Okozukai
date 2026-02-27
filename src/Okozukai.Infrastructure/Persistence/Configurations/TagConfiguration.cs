using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Okozukai.Domain.Transactions;

namespace Okozukai.Infrastructure.Persistence.Configurations;

internal sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(60);

        builder.Property(x => x.Color)
            .IsRequired()
            .HasMaxLength(7)
            .HasDefaultValue("#6366f1");

        builder.HasIndex(x => x.Name).IsUnique();
    }
}
