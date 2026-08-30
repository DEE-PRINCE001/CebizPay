using CebizPay.Domain.Payments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Payments;

/// <summary>
/// EF Core configuration for the <see cref="FundingTransaction"/> entity.
/// </summary>
public sealed class FundingTransactionConfiguration : IEntityTypeConfiguration<FundingTransaction>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<FundingTransaction> builder)
    {
        builder.ToTable("FundingTransactions");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.WalletId)
            .IsRequired();

        builder.Property(f => f.VirtualAccountId);

        builder.Property(f => f.ExternalFundingAccountId);

        builder.Property(f => f.LedgerTransactionId);

        builder.Property(f => f.Provider)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(f => f.ProviderTransactionReference)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(f => f.ProviderEventReference)
            .HasMaxLength(128);

        builder.Property(f => f.FundingChannel)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(f => f.Amount)
            .IsRequired()
            .HasColumnType("numeric(18,4)");

        builder.Property(f => f.FeeAmount)
            .IsRequired()
            .HasColumnType("numeric(18,4)")
            .HasDefaultValue(0m);

        builder.Property(f => f.NetCreditedAmount)
            .IsRequired()
            .HasColumnType("numeric(18,4)")
            .HasDefaultValue(0m);

        builder.Property(f => f.ProviderFeeAmount)
            .IsRequired()
            .HasColumnType("numeric(18,4)")
            .HasDefaultValue(0m);

        builder.Property(f => f.FeePolicyId);

        builder.Property(f => f.FeePolicyVersion);

        builder.Property(f => f.FeeBearer)
            .HasConversion<int>();

        builder.Property(f => f.Currency)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(f => f.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(f => f.FailureReason)
            .HasMaxLength(500);

        builder.Property(f => f.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(f => f.CompletedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(f => f.FailedAtUtc)
            .HasColumnType("timestamp with time zone");

        // Indexes
        // 1. Uniqueness of provider transaction reference per provider
        builder.HasIndex(f => new { f.Provider, f.ProviderTransactionReference })
            .IsUnique()
            .HasDatabaseName("IX_FundingTransactions_Provider_ProviderTransactionReference");

        // 2. Query indexes
        builder.HasIndex(f => f.WalletId)
            .HasDatabaseName("IX_FundingTransactions_WalletId");

        builder.HasIndex(f => f.VirtualAccountId)
            .HasDatabaseName("IX_FundingTransactions_VirtualAccountId");

        builder.HasIndex(f => f.ExternalFundingAccountId)
            .HasDatabaseName("IX_FundingTransactions_ExternalFundingAccountId");

        builder.HasIndex(f => f.Status)
            .HasDatabaseName("IX_FundingTransactions_Status");

        builder.HasIndex(f => f.CreatedAtUtc)
            .HasDatabaseName("IX_FundingTransactions_CreatedAtUtc");
    }
}
