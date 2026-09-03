using CebizPay.Domain.Communication.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for NotificationDeliveryRecord entity.
/// </summary>
public sealed class NotificationDeliveryRecordConfiguration : IEntityTypeConfiguration<NotificationDeliveryRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<NotificationDeliveryRecord> builder)
    {
        builder.ToTable("NotificationDeliveryRecords");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.RecipientId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Channel)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.FailureReason)
            .HasMaxLength(1000);

        builder.Property(x => x.AttemptedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Authoritative deduplication constraint: exactly one delivery attempt per event, recipient, category, and channel
        builder.HasIndex(x => new { x.EventId, x.RecipientId, x.Type, x.Channel }).IsUnique();

        // Rate limiting index to quickly calculate outbound message volumes
        builder.HasIndex(x => new { x.RecipientId, x.AttemptedAtUtc });
    }
}
