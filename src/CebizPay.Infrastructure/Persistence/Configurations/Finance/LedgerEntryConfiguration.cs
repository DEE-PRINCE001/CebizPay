using CebizPay.Domain.Finance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Finance;

/// <summary>
/// EF Core configuration for <see cref="LedgerEntry"/> entity.
/// </summary>
public sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("LedgerEntries", t =>
        {
            t.HasCheckConstraint("CK_LedgerEntries_Amount_Positive", "\"Amount\" > 0");
        });

        builder.HasKey(le => le.Id);

        builder.Property(le => le.LedgerTransactionId)
            .IsRequired();

        builder.Property(le => le.LedgerAccountId)
            .IsRequired();

        builder.Property(le => le.Direction)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(le => le.Amount)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(le => le.Currency)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(le => le.Sequence)
            .IsRequired();

        builder.Property(le => le.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(le => le.LedgerTransactionId);
        builder.HasIndex(le => new { le.LedgerAccountId, le.CreatedAtUtc });
    }
}
