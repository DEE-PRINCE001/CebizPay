using CebizPay.Domain.Finance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Finance;

/// <summary>
/// EF Core configuration for the <see cref="PlatformFeePolicy"/> entity.
/// </summary>
public sealed class PlatformFeePolicyConfiguration : IEntityTypeConfiguration<PlatformFeePolicy>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PlatformFeePolicy> builder)
    {
        builder.ToTable("PlatformFeePolicies");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.OperationType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(p => p.CalculationMethod)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(p => p.FeeBearer)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(p => p.FixedAmount)
            .HasPrecision(18, 4);

        builder.Property(p => p.PercentageRate)
            .HasPrecision(18, 6);

        builder.Property(p => p.MinimumFee)
            .HasPrecision(18, 4);

        builder.Property(p => p.MaximumFee)
            .HasPrecision(18, 4);

        builder.Property(p => p.Currency)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(p => p.Version)
            .IsRequired();

        builder.Property(p => p.IsEnabled)
            .IsRequired();

        builder.Property(p => p.EffectiveFromUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(p => p.DeactivatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(p => p.CreatedByUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(p => p.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(p => p.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");

        // Indexes
        // 1. Unique version number per operation type
        builder.HasIndex(p => new { p.OperationType, p.Version })
            .IsUnique()
            .HasDatabaseName("IX_PlatformFeePolicies_OperationType_Version");

        // 2. Strict Database-level invariant: At most ONE active policy per operation type
        builder.HasIndex(p => p.OperationType)
            .IsUnique()
            .HasFilter("\"IsEnabled\" = TRUE")
            .HasDatabaseName("IX_PlatformFeePolicies_OperationType_Active");

        // 3. Query filter index on IsEnabled
        builder.HasIndex(p => p.IsEnabled)
            .HasDatabaseName("IX_PlatformFeePolicies_IsEnabled");
    }
}
