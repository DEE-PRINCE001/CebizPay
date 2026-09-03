using CebizPay.Domain.Referrals.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for ReferralRelationship aggregate.
/// </summary>
public sealed class ReferralRelationshipConfiguration : IEntityTypeConfiguration<ReferralRelationship>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ReferralRelationship> builder)
    {
        builder.ToTable("ReferralRelationships");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ReferrerUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.ReferredUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.ReferralCode)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.QualificationStatus)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.RewardEligibility)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.RegisteredAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.QualifiedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.QualifyingDepositReference)
            .HasMaxLength(150);

        builder.Property(x => x.QualifyingDepositAmount)
            .HasPrecision(18, 2);

        builder.Property(x => x.RiskReviewNotes)
            .HasMaxLength(1000);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Unique constraint: A referred user can be associated with at most one referring relationship
        builder.HasIndex(x => x.ReferredUserId).IsUnique();

        // Query index for referrer lookup and dashboard
        builder.HasIndex(x => x.ReferrerUserId);

        // Query index for qualification lookups
        builder.HasIndex(x => new { x.ReferrerUserId, x.QualificationStatus });
    }
}
