using CebizPay.Domain.Thrift.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Thrift;

/// <summary>
/// Entity configuration for ThriftPayout.
/// </summary>
public class ThriftPayoutConfiguration : IEntityTypeConfiguration<ThriftPayout>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ThriftPayout> builder)
    {
        builder.ToTable("ThriftPayouts");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.BeneficiaryUserId)
            .HasMaxLength(256)
            .IsRequired();

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

        builder.Property(e => e.PaidAtUtc)
            .IsRequired();

        // Exactly one payout record per cycle
        builder.HasIndex(e => e.ThriftCycleId)
            .IsUnique();

        builder.HasIndex(e => e.ThriftGroupId);
        builder.HasIndex(e => e.BeneficiaryUserId);
        builder.HasIndex(e => e.IdempotencyKey);
    }
}
