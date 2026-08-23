using CebizPay.Domain.Savings.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Savings;

/// <summary>
/// Entity configuration for SavingsInterestAccrual.
/// </summary>
public class SavingsInterestAccrualConfiguration : IEntityTypeConfiguration<SavingsInterestAccrual>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SavingsInterestAccrual> builder)
    {
        builder.ToTable("SavingsInterestAccruals");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.AccrualDate)
            .IsRequired();

        builder.Property(e => e.PrincipalBasis)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(e => e.Rate)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(e => e.Amount)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(e => e.PolicyVersion)
            .IsRequired();

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired();

        // Idempotent uniqueness: one accrual record per savings account per calendar date
        builder.HasIndex(e => new { e.SavingsAccountId, e.AccrualDate })
            .IsUnique();
    }
}
