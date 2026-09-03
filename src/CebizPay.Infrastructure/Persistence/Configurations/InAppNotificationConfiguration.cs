using CebizPay.Domain.Communication.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for InAppNotification aggregate.
/// </summary>
public sealed class InAppNotificationConfiguration : IEntityTypeConfiguration<InAppNotification>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<InAppNotification> builder)
    {
        builder.ToTable("InAppNotifications");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Body)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Priority)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.DeepLink)
            .HasMaxLength(500);

        builder.Property(x => x.EventId)
            .HasMaxLength(100);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.ReadAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.ExpiresAtUtc)
            .HasColumnType("timestamp with time zone");

        // Optimal indexes for user inbox and unread count queries
        builder.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.UserId, x.ReadAtUtc });
        builder.HasIndex(x => new { x.OrganizationId, x.CreatedAtUtc });
    }
}
