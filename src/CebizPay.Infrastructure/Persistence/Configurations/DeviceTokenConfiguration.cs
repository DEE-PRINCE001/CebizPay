using CebizPay.Domain.Communication.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for DeviceToken entity.
/// </summary>
public sealed class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.ToTable("DeviceTokens");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.Token)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Platform)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.DeviceModel)
            .HasMaxLength(150);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.LastUsedAtUtc)
            .HasColumnType("timestamp with time zone");

        // Unique index on raw FCM registration token
        builder.HasIndex(x => x.Token).IsUnique();

        // Query index for resolving active devices for a recipient user
        builder.HasIndex(x => new { x.UserId, x.IsActive });
    }
}
