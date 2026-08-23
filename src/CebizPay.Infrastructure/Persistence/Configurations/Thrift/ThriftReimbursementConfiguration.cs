using CebizPay.Domain.Thrift.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Thrift;

/// <summary>
/// Entity configuration for ThriftReimbursement.
/// </summary>
public class ThriftReimbursementConfiguration : IEntityTypeConfiguration<ThriftReimbursement>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ThriftReimbursement> builder)
    {
        builder.ToTable("ThriftReimbursements");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.NetRefundAmount)
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

        builder.Property(e => e.ReimbursedAtUtc)
            .IsRequired();

        // Exactly one reimbursement per member
        builder.HasIndex(e => e.MemberId)
            .IsUnique();

        builder.HasIndex(e => e.ThriftGroupId);
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.IdempotencyKey);
    }
}
