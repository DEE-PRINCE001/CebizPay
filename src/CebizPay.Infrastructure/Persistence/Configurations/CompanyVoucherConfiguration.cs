using CebizPay.Domain.Erp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core entity configuration for <see cref="CompanyVoucher"/>.
/// </summary>
public sealed class CompanyVoucherConfiguration : IEntityTypeConfiguration<CompanyVoucher>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<CompanyVoucher> builder)
    {
        builder.ToTable("CompanyVouchers");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.VoucherNumber)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(v => v.PayeeName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(v => v.PayeeDetails)
            .HasMaxLength(500);

        builder.Property(v => v.Purpose)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(v => v.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(v => v.CreatedByUserId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(v => v.ApprovedByUserId)
            .HasMaxLength(100);

        builder.Property(v => v.Reference)
            .HasMaxLength(100);

        builder.Property(v => v.Notes)
            .HasMaxLength(1000);

        builder.HasIndex(v => new { v.OrganizationId, v.VoucherNumber })
            .IsUnique();

        builder.HasIndex(v => new { v.OrganizationId, v.Status });
        builder.HasIndex(v => new { v.OrganizationId, v.CreatedAtUtc });
    }
}
