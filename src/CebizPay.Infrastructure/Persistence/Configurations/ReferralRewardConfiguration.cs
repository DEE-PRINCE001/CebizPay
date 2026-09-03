using CebizPay.Domain.Referrals.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for ReferralReward entity.
/// </summary>
public sealed class ReferralRewardConfiguration : IEntityTypeConfiguration<ReferralReward>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ReferralReward> builder)
    {
        builder.ToTable("ReferralRewards");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ReferralRelationshipId)
            .IsRequired();

        builder.Property(x => x.ReferrerUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.ReferredUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.EligibleAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.LedgerTransactionReference)
            .HasMaxLength(150);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Exactly one reward per referral relationship
        builder.HasIndex(x => x.ReferralRelationshipId).IsUnique();

        // Query index for referrer lookup
        builder.HasIndex(x => x.ReferrerUserId);

        // Query index for status
        builder.HasIndex(x => new { x.ReferrerUserId, x.Status });
    }
}
