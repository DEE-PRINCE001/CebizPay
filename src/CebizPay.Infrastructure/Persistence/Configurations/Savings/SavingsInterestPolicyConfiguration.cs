using CebizPay.Domain.Savings.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Savings;

/// <summary>
/// Entity configuration for SavingsInterestPolicy.
/// </summary>
public class SavingsInterestPolicyConfiguration : IEntityTypeConfiguration<SavingsInterestPolicy>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SavingsInterestPolicy> builder)
    {
        builder.ToTable("SavingsInterestPolicies");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PlanType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Mode)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.AnnualRate)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(e => e.Version)
            .IsRequired();

        builder.Property(e => e.EffectiveFromUtc)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.HasIndex(e => new { e.PlanType, e.Version })
            .IsUnique();

        builder.HasIndex(e => new { e.PlanType, e.IsActive });
    }
}
