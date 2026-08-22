using CebizPay.Domain.Entities;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Payroll.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Payroll;

/// <summary>
/// EF Core configuration for <see cref="PaymentVoucher"/> entity.
/// </summary>
public sealed class PaymentVoucherConfiguration : IEntityTypeConfiguration<PaymentVoucher>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PaymentVoucher> builder)
    {
        builder.ToTable("PaymentVouchers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.VoucherReference)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(x => x.VoucherReference)
            .IsUnique();

        builder.Property(x => x.PayrollBatchId)
            .IsRequired();

        builder.Property(x => x.PayrollItemId)
            .IsRequired();

        builder.Property(x => x.LedgerTransactionId)
            .IsRequired();

        builder.Property(x => x.OrganizationId)
            .IsRequired();

        builder.Property(x => x.EmployeeUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.EmployeeName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.GrossPay)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.Deductions)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.NetPay)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.BankName)
            .HasMaxLength(128);

        builder.Property(x => x.Remarks)
            .HasMaxLength(500);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.OrganizationId, x.CreatedAtUtc });
        builder.HasIndex(x => x.PayrollBatchId);
        builder.HasIndex(x => x.PayrollItemId);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<LedgerTransaction>()
            .WithMany()
            .HasForeignKey(x => x.LedgerTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
