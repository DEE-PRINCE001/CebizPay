using CebizPay.Domain.Referrals.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for ReferralCode entity.
/// </summary>
public sealed class ReferralCodeConfiguration : IEntityTypeConfiguration<ReferralCode>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ReferralCode> builder)
    {
        builder.ToTable("ReferralCodes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Unique index on code string
        builder.HasIndex(x => x.Code).IsUnique();

        // Query index for user's active code
        builder.HasIndex(x => new { x.UserId, x.IsActive });
    }
}
