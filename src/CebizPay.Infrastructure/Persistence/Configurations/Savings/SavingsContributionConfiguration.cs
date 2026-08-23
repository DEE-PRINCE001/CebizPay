using CebizPay.Domain.Savings.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Savings;

/// <summary>
/// Entity configuration for SavingsContribution.
/// </summary>
public class SavingsContributionConfiguration : IEntityTypeConfiguration<SavingsContribution>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SavingsContribution> builder)
    {
        builder.ToTable("SavingsContributions");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(e => e.Currency)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.LedgerTransactionId)
            .IsRequired();

        builder.Property(e => e.IdempotencyKey)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(e => e.SavingsAccountId);
        builder.HasIndex(e => e.IdempotencyKey);
    }
}
