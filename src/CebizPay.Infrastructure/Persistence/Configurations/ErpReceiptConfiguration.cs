using CebizPay.Domain.Erp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core entity configuration for <see cref="ErpReceipt"/>.
/// </summary>
public sealed class ErpReceiptConfiguration : IEntityTypeConfiguration<ErpReceipt>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ErpReceipt> builder)
    {
        builder.ToTable("ErpReceipts");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.ReceiptNumber)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(r => r.Reference)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.CreatedByUserId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.Notes)
            .HasMaxLength(1000);

        builder.Property(r => r.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasIndex(r => new { r.OrganizationId, r.ReceiptNumber })
            .IsUnique();

        // Exactly one receipt per invoice
        builder.HasIndex(r => r.InvoiceId)
            .IsUnique();

        builder.HasIndex(r => new { r.OrganizationId, r.CustomerId });
    }
}
