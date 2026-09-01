#pragma warning disable CS1591
using CebizPay.Domain.Payments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Payments;

public sealed class ReconciliationRecordConfiguration : IEntityTypeConfiguration<ReconciliationRecord>
{
    public void Configure(EntityTypeBuilder<ReconciliationRecord> builder)
    {
        builder.ToTable("ReconciliationRecords");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.ReconciliationType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.SourceReference)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(r => r.Provider)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(r => r.ProviderReference)
            .HasMaxLength(128);

        builder.Property(r => r.ExpectedAmount)
            .HasPrecision(18, 4);

        builder.Property(r => r.ReconciledAmount)
            .HasPrecision(18, 4);

        builder.Property(r => r.Currency)
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(r => r.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.DiscrepancyReason)
            .HasMaxLength(1000);

        builder.Property(r => r.SafeMetadata)
            .HasMaxLength(2000);

        builder.Property(r => r.AttemptCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(r => r.MaxAttempts)
            .IsRequired()
            .HasDefaultValue(5);

        builder.Property(r => r.NextPollAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(r => r.LastPolledAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(r => r.ResolvedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(r => r.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(r => r.UpdatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        // Indexes
        builder.HasIndex(r => new { r.Status, r.NextPollAtUtc, r.CreatedAtUtc });
        builder.HasIndex(r => r.SourceReference);
        builder.HasIndex(r => new { r.Provider, r.ProviderReference });
    }
}
