using CebizPay.Domain.Savings.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Savings;

/// <summary>
/// Entity configuration for SavingsPlan.
/// </summary>
public class SavingsPlanConfiguration : IEntityTypeConfiguration<SavingsPlan>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SavingsPlan> builder)
    {
        builder.ToTable("SavingsPlans");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.CreatedByUserId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.OwnerType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.PlanType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(1000);

        builder.Property(e => e.Currency)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.InterestRate)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(e => e.MinimumAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(e => e.MaximumAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(e => e.TargetAmount)
            .HasPrecision(18, 2);

        builder.Property(e => e.ContributionAmount)
            .HasPrecision(18, 2);

        builder.Property(e => e.ContributionFrequency)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(e => e.OrganizationId);
        builder.HasIndex(e => new { e.PlanType, e.IsActive });
    }
}
