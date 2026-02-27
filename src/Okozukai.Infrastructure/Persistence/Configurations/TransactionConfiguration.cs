using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Okozukai.Domain.Transactions;

namespace Okozukai.Infrastructure.Persistence.Configurations;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Type)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(t => t.Amount)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(t => t.OccurredAt)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.Property(t => t.Note)
            .HasMaxLength(500);

        builder.HasOne(t => t.Journal)
            .WithMany()
            .HasForeignKey(t => t.JournalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Tags)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "TransactionTags",
                right => right
                    .HasOne<Tag>()
                    .WithMany()
                    .HasForeignKey("TagId")
                    .OnDelete(DeleteBehavior.Cascade),
                left => left
                    .HasOne<Transaction>()
                    .WithMany()
                    .HasForeignKey("TransactionId")
                    .OnDelete(DeleteBehavior.Cascade),
                j =>
                {
                    j.ToTable("TransactionTags");
                    j.HasKey("TransactionId", "TagId");
                    j.HasIndex("TagId");
                });
    }
}
