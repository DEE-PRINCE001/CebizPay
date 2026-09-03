using CebizPay.Domain.Referrals.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for ReferralSetting entity.
/// </summary>
public sealed class ReferralSettingConfiguration : IEntityTypeConfiguration<ReferralSetting>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ReferralSetting> builder)
    {
        builder.ToTable("ReferralSettings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RewardAmountPerSuccessfulReferral)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.MaximumSuccessfulReferralsPerUser)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.Version)
            .IsRequired();

        builder.Property(x => x.UpdatedBy)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(x => x.IsActive);
    }
}
