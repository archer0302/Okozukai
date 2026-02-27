using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Okozukai.Domain.Transactions;

namespace Okozukai.Infrastructure.Persistence.Configurations;

internal sealed class JournalConfiguration : IEntityTypeConfiguration<Journal>
{
    public void Configure(EntityTypeBuilder<Journal> builder)
    {
        builder.ToTable("Journals");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(j => j.PrimaryCurrency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(j => j.IsClosed)
            .IsRequired();

        builder.Property(j => j.CreatedAt)
            .IsRequired();
    }
}
