using CebizPay.Domain.Finance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Finance;

/// <summary>
/// EF Core configuration for the <see cref="ExternalFundingAccount"/> entity.
/// </summary>
public sealed class ExternalFundingAccountConfiguration : IEntityTypeConfiguration<ExternalFundingAccount>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ExternalFundingAccount> builder)
    {
        builder.ToTable("ExternalFundingAccounts");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.WalletId)
            .IsRequired();

        builder.Property(e => e.Provider)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.AccountNumber)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(e => e.AccountName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.BankCode)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(e => e.BankName)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(e => e.Currency)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.IsPrimary)
            .IsRequired();

        builder.Property(e => e.ProviderCustomerReference)
            .HasMaxLength(128);

        builder.Property(e => e.ProviderAccountReference)
            .HasMaxLength(128);

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");

        // Relationships: Non-destructive foreign key constraint
        builder.HasOne(e => e.Wallet)
            .WithMany(w => w.ExternalFundingAccounts)
            .HasForeignKey(e => e.WalletId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        // 1. Foreign key index on WalletId
        builder.HasIndex(e => e.WalletId)
            .HasDatabaseName("IX_ExternalFundingAccounts_WalletId");

        // 2. Unique account number per provider
        builder.HasIndex(e => new { e.Provider, e.AccountNumber })
            .IsUnique()
            .HasDatabaseName("IX_ExternalFundingAccounts_Provider_AccountNumber");

        // 3. Unique provider account reference when populated
        builder.HasIndex(e => new { e.Provider, e.ProviderAccountReference })
            .IsUnique()
            .HasFilter("\"ProviderAccountReference\" IS NOT NULL")
            .HasDatabaseName("IX_ExternalFundingAccounts_Provider_ProviderAccountReference");

        // 4. Strict Database-level invariant: At most ONE primary account per wallet
        builder.HasIndex(e => e.WalletId)
            .IsUnique()
            .HasFilter("\"IsPrimary\" = TRUE")
            .HasDatabaseName("IX_ExternalFundingAccounts_WalletId_IsPrimary");

        // 5. Query filter index on status
        builder.HasIndex(e => e.Status)
            .HasDatabaseName("IX_ExternalFundingAccounts_Status");
    }
}
