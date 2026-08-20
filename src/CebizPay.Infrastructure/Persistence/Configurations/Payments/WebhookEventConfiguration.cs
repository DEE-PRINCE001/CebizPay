using CebizPay.Domain.Payments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Payments;

/// <summary>
/// EF Core configuration for the <see cref="WebhookEvent"/> entity.
/// </summary>
public sealed class WebhookEventConfiguration : IEntityTypeConfiguration<WebhookEvent>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WebhookEvent> builder)
    {
        builder.ToTable("WebhookEvents");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Provider)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(w => w.ProviderEventId)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(w => w.EventType)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(w => w.PayloadHash)
            .HasMaxLength(128);

        builder.Property(w => w.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(w => w.ProcessingError)
            .HasMaxLength(1000);

        builder.Property(w => w.SafeMetadata)
            .HasMaxLength(2000);

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

        // Indexes & Unique Constraints
        // 1. Mandatory deduplication unique constraint: Provider + ProviderEventId
        builder.HasIndex(w => new { w.Provider, w.ProviderEventId })
            .IsUnique()
            .HasDatabaseName("IX_WebhookEvents_Provider_ProviderEventId");

        // 2. Query performance indexes
        builder.HasIndex(w => w.Status)
            .HasDatabaseName("IX_WebhookEvents_Status");

        builder.HasIndex(w => w.PaymentAttemptId)
            .HasDatabaseName("IX_WebhookEvents_PaymentAttemptId");

        builder.HasIndex(w => w.ReceivedAtUtc)
            .HasDatabaseName("IX_WebhookEvents_ReceivedAtUtc");
    }
}
