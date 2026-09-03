using CebizPay.Domain.Communication.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for UserNotificationPreference entity.
/// </summary>
public sealed class UserNotificationPreferenceConfiguration : IEntityTypeConfiguration<UserNotificationPreference>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<UserNotificationPreference> builder)
    {
        builder.ToTable("UserNotificationPreferences");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.InAppEnabled)
            .IsRequired();

        builder.Property(x => x.PushEnabled)
            .IsRequired();

        builder.Property(x => x.EmailEnabled)
            .IsRequired();

        builder.Property(x => x.SmsEnabled)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Exactly one preference row per user and notification type
        builder.HasIndex(x => new { x.UserId, x.Type }).IsUnique();
    }
}
