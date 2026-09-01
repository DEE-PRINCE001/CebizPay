#pragma warning disable CS1591
using CebizPay.Domain.Compliance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Compliance;

public sealed class ComplianceWebhookEventConfiguration : IEntityTypeConfiguration<ComplianceWebhookEvent>
{
    public void Configure(EntityTypeBuilder<ComplianceWebhookEvent> builder)
    {
        builder.ToTable("ComplianceWebhookEvents");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Provider)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(w => w.ProviderEventId)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(w => w.EventType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(w => w.PayloadHash)
            .HasMaxLength(64);

        builder.Property(w => w.CorrelationReference)
            .HasMaxLength(150);

        builder.Property(w => w.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(w => w.ProcessingError)
            .HasMaxLength(1000);

        builder.Property(w => w.SafeMetadata)
            .HasMaxLength(2000);

        builder.Property(w => w.AttemptCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(w => w.MaxAttempts)
            .IsRequired()
            .HasDefaultValue(5);

        builder.Property(w => w.NextRetryAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(w => w.LockedUntilUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(w => w.LockedBy)
            .HasMaxLength(128);

        builder.Property(w => w.ReceivedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(w => w.ProcessedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(w => w.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(w => w.UpdatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        // Indexes
        builder.HasIndex(w => new { w.Provider, w.ProviderEventId })
            .IsUnique();

        builder.HasIndex(w => w.PayloadHash);

        builder.HasIndex(w => new { w.Status, w.NextRetryAtUtc, w.CreatedAtUtc });

        builder.HasIndex(w => w.CorrelationReference);
    }
}
