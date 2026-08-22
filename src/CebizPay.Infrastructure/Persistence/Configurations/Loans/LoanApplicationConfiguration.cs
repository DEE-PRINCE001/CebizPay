using CebizPay.Domain.Loans.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Loans;

/// <summary>
/// EF Core configuration for <see cref="LoanApplication"/> entity.
/// </summary>
public sealed class LoanApplicationConfiguration : IEntityTypeConfiguration<LoanApplication>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<LoanApplication> builder)
    {
        builder.ToTable("LoanApplications");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ApplicationReference)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.OrganizationId)
            .IsRequired();

        builder.Property(x => x.LoanPlanId)
            .IsRequired();

        builder.Property(x => x.ApplicantUserId)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.ApplicantName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.RequestedAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.InterestRateSnapshot)
            .HasPrecision(8, 4)
            .IsRequired();

        builder.Property(x => x.DurationMonths)
            .IsRequired();

        builder.Property(x => x.RepaymentFrequency)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.ComputedMonthlyPayment)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.ComputedTotalInterest)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.ComputedTotalRepayment)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.VerifiedSalarySnapshot)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.ExistingMonthlyDebtSnapshot)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.ProposedMonthlyPaymentSnapshot)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.TotalMonthlyDebtSnapshot)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.DebtToIncomeRatioSnapshot)
            .HasPrecision(8, 4)
            .IsRequired();

        builder.Property(x => x.IsDtiCompliantSnapshot)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.UnderwritingReason)
            .HasMaxLength(1000);

        builder.Property(x => x.DeclinedReason)
            .HasMaxLength(1000);

        builder.Property(x => x.DeciderUserId)
            .HasMaxLength(128);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.DecidedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => x.ApplicationReference)
            .IsUnique();

        builder.HasIndex(x => new { x.OrganizationId, x.Status });
        builder.HasIndex(x => new { x.ApplicantUserId, x.Status });
    }
}
