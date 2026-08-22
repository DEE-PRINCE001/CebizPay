using CebizPay.Domain.Entities;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Payroll.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Payroll;

/// <summary>
/// EF Core configuration for <see cref="PayrollItem"/> entity.
/// </summary>
public sealed class PayrollItemConfiguration : IEntityTypeConfiguration<PayrollItem>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PayrollItem> builder)
    {
        builder.ToTable("PayrollItems");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PayrollBatchId)
            .IsRequired();

        builder.Property(x => x.OrganizationId)
            .IsRequired();

        builder.Property(x => x.EmployeeUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.EmployeeName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.EmployeeEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.Currency)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.GrossPay)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.TotalDeductions)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.NetPay)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.DeductionsDetailJson)
            .HasColumnType("text");

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.ClaimedByWorkerId)
            .HasMaxLength(128);

        builder.Property(x => x.ClaimedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.CurrentAttemptNumber)
            .IsRequired();

        builder.Property(x => x.LastFailureCode)
            .HasMaxLength(64);

        builder.Property(x => x.LastFailureReason)
            .HasMaxLength(1000);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.PayrollBatchId, x.EmployeeUserId })
            .IsUnique();

        builder.HasIndex(x => new { x.OrganizationId, x.Status });
        builder.HasIndex(x => new { x.PayrollBatchId, x.Status });
        builder.HasIndex(x => new { x.Status, x.ClaimedAtUtc });

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<LedgerTransaction>()
            .WithMany()
            .HasForeignKey(x => x.LedgerTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Attempts)
            .WithOne()
            .HasForeignKey(x => x.PayrollItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
