using CebizPay.Domain.Loans.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Loans;

/// <summary>
/// EF Core configuration for <see cref="LoanContract"/> entity.
/// </summary>
public sealed class LoanContractConfiguration : IEntityTypeConfiguration<LoanContract>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<LoanContract> builder)
    {
        builder.ToTable("LoanContracts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ContractReference)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.OrganizationId)
            .IsRequired();

        builder.Property(x => x.BorrowerUserId)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.BorrowerName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.LoanType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.OriginalPrincipal)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.InterestRate)
            .HasPrecision(8, 4)
            .IsRequired();

        builder.Property(x => x.TotalInterest)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.TotalRepayment)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.RepaymentFrequency)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.NumberOfInstallments)
            .IsRequired();

        builder.Property(x => x.MonthlyInstallmentAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.OutstandingPrincipal)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.TotalAmountPaid)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.StartDate)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.ExpectedEndDate)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.DisbursedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.ConvertedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.ConversionReason)
            .HasMaxLength(500);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => x.ContractReference)
            .IsUnique();

        builder.HasIndex(x => new { x.OrganizationId, x.Status });
        builder.HasIndex(x => new { x.BorrowerUserId, x.Status });
        builder.HasIndex(x => new { x.OrganizationId, x.BorrowerUserId, x.LoanType, x.Status });

        builder.HasMany(x => x.RepaymentSchedule)
            .WithOne()
            .HasForeignKey(x => x.LoanContractId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
