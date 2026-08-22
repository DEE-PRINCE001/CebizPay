using CebizPay.Domain.Loans.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Loans;

/// <summary>
/// EF Core configuration for <see cref="LoanRepaymentScheduleItem"/> entity.
/// </summary>
public sealed class LoanRepaymentScheduleItemConfiguration : IEntityTypeConfiguration<LoanRepaymentScheduleItem>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<LoanRepaymentScheduleItem> builder)
    {
        builder.ToTable("LoanRepaymentScheduleItems");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.LoanContractId)
            .IsRequired();

        builder.Property(x => x.InstallmentNumber)
            .IsRequired();

        builder.Property(x => x.DueDate)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.ScheduledAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.PrincipalComponent)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.InterestComponent)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.PaidAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.PaidAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.MissedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.LoanContractId, x.InstallmentNumber })
            .IsUnique();

        builder.HasIndex(x => new { x.LoanContractId, x.Status });
        builder.HasIndex(x => new { x.DueDate, x.Status });
    }
}
