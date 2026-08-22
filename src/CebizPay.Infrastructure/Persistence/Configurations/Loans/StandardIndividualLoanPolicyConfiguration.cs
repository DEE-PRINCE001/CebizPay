using CebizPay.Domain.Loans.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Loans;

/// <summary>
/// EF Core configuration for <see cref="StandardIndividualLoanPolicy"/> entity.
/// </summary>
public sealed class StandardIndividualLoanPolicyConfiguration : IEntityTypeConfiguration<StandardIndividualLoanPolicy>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<StandardIndividualLoanPolicy> builder)
    {
        builder.ToTable("StandardIndividualLoanPolicies");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PolicyName)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.AnnualInterestRate)
            .HasPrecision(8, 4)
            .IsRequired();

        builder.Property(x => x.RepaymentFrequency)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.MaximumDurationMonths)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");
    }
}
