using CebizPay.Domain.Savings.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Savings;

/// <summary>
/// Entity configuration for SavingsAccount.
/// </summary>
public class SavingsAccountConfiguration : IEntityTypeConfiguration<SavingsAccount>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SavingsAccount> builder)
    {
        builder.ToTable("SavingsAccounts");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.OwnerUserId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.Currency)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.PlanType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.PrincipalBalance)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(e => e.AccruedInterest)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(e => e.TotalInterestWithdrawn)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.InterestRateSnapshot)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(e => e.PenaltyRateSnapshot)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(e => e.TargetAmount)
            .HasPrecision(18, 2);

        builder.Property(e => e.ContributionAmount)
            .HasPrecision(18, 2);

        builder.Property(e => e.ContributionFrequency)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.EarlyWithdrawalPenaltyAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(e => e.ForfeitedInterestAmount)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(e => e.StartDateUtc)
            .IsRequired();

        builder.Property(e => e.MaturityDateUtc)
            .IsRequired();

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired();

        builder.HasMany(e => e.Contributions)
            .WithOne()
            .HasForeignKey(c => c.SavingsAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.Contributions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(e => e.InterestAccruals)
            .WithOne()
            .HasForeignKey(a => a.SavingsAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.InterestAccruals)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(e => e.SavingsPlanId);
        builder.HasIndex(e => e.OwnerUserId);
        builder.HasIndex(e => e.OrganizationId);
        builder.HasIndex(e => e.Status);
    }
}
