using CebizPay.Domain.Finance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Finance;

/// <summary>
/// EF Core configuration for the <see cref="BankTransfer"/> aggregate entity.
/// </summary>
public sealed class BankTransferConfiguration : IEntityTypeConfiguration<BankTransfer>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BankTransfer> builder)
    {
        builder.ToTable("BankTransfers");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.LedgerTransactionId)
            .IsRequired();

        builder.Property(t => t.SenderWalletId)
            .IsRequired();

        builder.Property(t => t.DestinationBankCode)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(t => t.DestinationAccountNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(t => t.DestinationAccountName)
            .HasMaxLength(256);

        builder.Property(t => t.Amount)
            .IsRequired()
            .HasColumnType("numeric(18,4)");

        builder.Property(t => t.Currency)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(t => t.FeeAmount)
            .IsRequired()
            .HasColumnType("numeric(18,4)");

        builder.Property(t => t.TotalDebited)
            .IsRequired()
            .HasColumnType("numeric(18,4)");

        builder.Property(t => t.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(t => t.Reference)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(t => t.FailureReason)
            .HasMaxLength(500);

        builder.Property(t => t.ProviderReference)
            .HasMaxLength(128);

        builder.Property(t => t.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(t => t.UpdatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(t => t.CompletedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(t => t.FailedAtUtc)
            .HasColumnType("timestamp with time zone");

        // Indexes for performance and uniqueness
        builder.HasIndex(t => t.Reference)
            .IsUnique()
            .HasDatabaseName("IX_BankTransfers_Reference");

        builder.HasIndex(t => t.LedgerTransactionId)
            .IsUnique()
            .HasDatabaseName("IX_BankTransfers_LedgerTransactionId");

        builder.HasIndex(t => t.SenderWalletId)
            .HasDatabaseName("IX_BankTransfers_SenderWalletId");

        builder.HasIndex(t => t.Status)
            .HasDatabaseName("IX_BankTransfers_Status");

        builder.HasIndex(t => t.CreatedAtUtc)
            .HasDatabaseName("IX_BankTransfers_CreatedAtUtc");

        builder.HasIndex(t => new { t.DestinationBankCode, t.DestinationAccountNumber })
            .HasDatabaseName("IX_BankTransfers_DestinationBank_AccountNumber");
    }
}
