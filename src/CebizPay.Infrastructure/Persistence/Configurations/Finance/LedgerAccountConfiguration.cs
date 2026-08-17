using CebizPay.Domain.Finance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Finance;

/// <summary>
/// EF Core configuration for <see cref="LedgerAccount"/> entity.
/// </summary>
public sealed class LedgerAccountConfiguration : IEntityTypeConfiguration<LedgerAccount>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<LedgerAccount> builder)
    {
        builder.ToTable("LedgerAccounts");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.WalletId)
            .IsRequired(false);

        builder.Property(l => l.AccountName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(l => l.Currency)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(l => l.AccountType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(l => l.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(l => l.CreatedAtUtc)
            .IsRequired();

        // 1 Wallet -> 1 LedgerAccount
        builder.HasIndex(l => l.WalletId)
            .IsUnique()
            .HasFilter("\"WalletId\" IS NOT NULL");

        // Unique system accounts per AccountType + Currency
        builder.HasIndex(l => new { l.AccountType, l.Currency })
            .IsUnique()
            .HasFilter("\"WalletId\" IS NULL");
    }
}
