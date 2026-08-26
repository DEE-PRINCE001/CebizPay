using CebizPay.Domain.Erp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core entity configuration for <see cref="ErpInvoice"/>.
/// </summary>
public sealed class ErpInvoiceConfiguration : IEntityTypeConfiguration<ErpInvoice>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ErpInvoice> builder)
    {
        builder.ToTable("ErpInvoices");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.InvoiceNumber)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(i => i.CreatedByUserId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(i => i.Notes)
            .HasMaxLength(2000);

        builder.Property(i => i.BillingContact)
            .HasMaxLength(255);

        builder.Property(i => i.VatRate)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(i => i.Subtotal)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(i => i.VatAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(i => i.TotalAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(i => i.PaidAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasIndex(i => new { i.OrganizationId, i.InvoiceNumber })
            .IsUnique();

        builder.HasIndex(i => new { i.OrganizationId, i.CustomerId });
        builder.HasIndex(i => new { i.OrganizationId, i.Status });
        builder.HasIndex(i => new { i.OrganizationId, i.DueDate });

        builder.HasMany(i => i.Items)
            .WithOne()
            .HasForeignKey(item => item.ErpInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(i => i.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>
/// EF Core entity configuration for <see cref="ErpInvoiceItem"/>.
/// </summary>
public sealed class ErpInvoiceItemConfiguration : IEntityTypeConfiguration<ErpInvoiceItem>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ErpInvoiceItem> builder)
    {
        builder.ToTable("ErpInvoiceItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(i => i.Quantity)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(i => i.UnitPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(i => i.TotalAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasIndex(i => i.ErpInvoiceId);
    }
}
