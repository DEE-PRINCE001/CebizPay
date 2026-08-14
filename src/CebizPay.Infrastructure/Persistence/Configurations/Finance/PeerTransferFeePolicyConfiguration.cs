using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Finance;

/// <summary>
/// EF Core configuration for the PeerTransferFeePolicy entity.
/// </summary>
public sealed class PeerTransferFeePolicyConfiguration : IEntityTypeConfiguration<PeerTransferFeePolicy>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PeerTransferFeePolicy> builder)
    {
        builder.ToTable("PeerTransferFeePolicies");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Mode)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(p => p.PercentageRate)
            .HasColumnType("numeric(18,6)");

        builder.Property(p => p.MinimumFee)
            .HasColumnType("numeric(18,4)");

        builder.Property(p => p.MaximumFee)
            .HasColumnType("numeric(18,4)");

        builder.Property(p => p.IsEnabled)
            .IsRequired();

        builder.Property(p => p.EffectiveFrom)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(p => p.Version)
            .IsRequired();

        builder.Property(p => p.CreatedByUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(p => p.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(p => p.DeactivatedAtUtc)
            .HasColumnType("timestamp with time zone");

        // Unique version index — each version is distinct across all policies
        builder.HasIndex(p => p.Version)
            .IsUnique()
            .HasDatabaseName("IX_PeerTransferFeePolicies_Version");

        // Index for fast active-policy lookup
        builder.HasIndex(p => new { p.IsEnabled, p.EffectiveFrom })
            .HasDatabaseName("IX_PeerTransferFeePolicies_IsEnabled_EffectiveFrom");
    }
}
