using CebizPay.Domain.Payments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Payments;

/// <summary>
/// EF Core configuration for the <see cref="CardRefund"/> entity.
/// </summary>
public sealed class CardRefundConfiguration : IEntityTypeConfiguration<CardRefund>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<CardRefund> builder)
    {
        builder.ToTable("CardRefunds");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.FundingTransactionId)
            .IsRequired();

        builder.Property(r => r.WalletId)
            .IsRequired();

        builder.Property(r => r.Provider)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.RefundReference)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(r => r.ProviderRefundReference)
            .HasMaxLength(128);

        builder.Property(r => r.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(r => r.Amount)
            .IsRequired()
            .HasColumnType("numeric(18,4)");

        builder.Property(r => r.Currency)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.Reason)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(r => r.LedgerTransactionId);

        builder.Property(r => r.FailureReason)
            .HasMaxLength(500);

        builder.Property(r => r.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(r => r.CompletedAtUtc)
            .HasColumnType("timestamp with time zone");

        // Indexes
        builder.HasIndex(r => r.RefundReference)
            .IsUnique()
            .HasDatabaseName("IX_CardRefunds_RefundReference");

        builder.HasIndex(r => r.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("IX_CardRefunds_IdempotencyKey");

        builder.HasIndex(r => r.FundingTransactionId)
            .HasDatabaseName("IX_CardRefunds_FundingTransactionId");

        builder.HasIndex(r => r.WalletId)
            .HasDatabaseName("IX_CardRefunds_WalletId");
    }
}
