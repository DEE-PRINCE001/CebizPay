#pragma warning disable CS1591
using CebizPay.Domain.Payments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Payments;

public sealed class RecoveryOutstandingRecordConfiguration : IEntityTypeConfiguration<RecoveryOutstandingRecord>
{
    public void Configure(EntityTypeBuilder<RecoveryOutstandingRecord> builder)
    {
        builder.ToTable("RecoveryOutstandingRecords");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.WalletId)
            .IsRequired();

        builder.Property(r => r.SourceTransactionType)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(r => r.SourceReference)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(r => r.Provider)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.AmountOwed)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(r => r.AmountRecovered)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(r => r.Currency)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(r => r.Reason)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(r => r.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.LastActionDetails)
            .HasMaxLength(1000);

        builder.Property(r => r.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(r => r.ResolvedAtUtc)
            .HasColumnType("timestamp with time zone");

        // Indexes
        builder.HasIndex(r => new { r.WalletId, r.Status });
        builder.HasIndex(r => r.SourceReference);
    }
}
