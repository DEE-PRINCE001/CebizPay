using CebizPay.Domain.Payments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Payments;

/// <summary>
/// EF Core configuration for the <see cref="PaymentAttempt"/> entity.
/// </summary>
public sealed class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttempt>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PaymentAttempt> builder)
    {
        builder.ToTable("PaymentAttempts");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.LedgerTransactionId)
            .IsRequired();

        builder.Property(p => p.Provider)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(p => p.AttemptNumber)
            .IsRequired();

        builder.Property(p => p.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(p => p.RequestReference)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(p => p.ProviderReference)
            .HasMaxLength(128);

        builder.Property(p => p.Amount)
            .IsRequired()
            .HasColumnType("numeric(18,4)");

        builder.Property(p => p.Currency)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(p => p.FailureCode)
            .HasMaxLength(64);

        builder.Property(p => p.FailureReason)
            .HasMaxLength(500);

        builder.Property(p => p.SafeMetadata)
            .HasMaxLength(2000);

        builder.Property(p => p.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(p => p.StartedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(p => p.CompletedAtUtc)
            .HasColumnType("timestamp with time zone");

        // Indexes & Constraints
        // 1. Uniqueness of attempt number per CebizPay ledger transaction
        builder.HasIndex(p => new { p.LedgerTransactionId, p.AttemptNumber })
            .IsUnique()
            .HasDatabaseName("IX_PaymentAttempts_LedgerTransactionId_AttemptNumber");

        // 2. Uniqueness of provider reference per provider (filtered when assigned)
        builder.HasIndex(p => new { p.Provider, p.ProviderReference })
            .IsUnique()
            .HasFilter("\"ProviderReference\" IS NOT NULL")
            .HasDatabaseName("IX_PaymentAttempts_Provider_ProviderReference");

        // 3. Unique client request reference sent to provider
        builder.HasIndex(p => p.RequestReference)
            .IsUnique()
            .HasDatabaseName("IX_PaymentAttempts_RequestReference");

        // 4. Query performance indexes
        builder.HasIndex(p => p.LedgerTransactionId)
            .HasDatabaseName("IX_PaymentAttempts_LedgerTransactionId");

        builder.HasIndex(p => p.Status)
            .HasDatabaseName("IX_PaymentAttempts_Status");

        builder.HasIndex(p => p.CreatedAtUtc)
            .HasDatabaseName("IX_PaymentAttempts_CreatedAtUtc");
    }
}
