using CebizPay.Domain.Loans.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Loans;

/// <summary>
/// EF Core configuration for <see cref="CorporateLoanPlan"/> entity.
/// </summary>
public sealed class CorporateLoanPlanConfiguration : IEntityTypeConfiguration<CorporateLoanPlan>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<CorporateLoanPlan> builder)
    {
        builder.ToTable("CorporateLoanPlans");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrganizationId)
            .IsRequired();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.MinimumAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.MaximumAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.InterestRate)
            .HasPrecision(8, 4)
            .IsRequired();

        builder.Property(x => x.MinimumDurationMonths)
            .IsRequired();

        builder.Property(x => x.MaximumDurationMonths)
            .IsRequired();

        builder.Property(x => x.RepaymentFrequency)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.MinimumMonthlySalary)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.OrganizationId, x.Name });
        builder.HasIndex(x => new { x.OrganizationId, x.IsActive });
    }
}
