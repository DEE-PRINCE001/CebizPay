using CebizPay.Domain.Erp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core entity configuration for <see cref="PurchaseOrder"/>.
/// </summary>
public sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("PurchaseOrders");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.OrderNumber)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(p => p.CreatedByUserId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.Notes)
            .HasMaxLength(2000);

        builder.Property(p => p.Subtotal)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.VatAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.TotalAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasIndex(p => new { p.OrganizationId, p.OrderNumber })
            .IsUnique();

        builder.HasIndex(p => new { p.OrganizationId, p.SupplierId });
        builder.HasIndex(p => new { p.OrganizationId, p.Status });

        builder.HasMany(p => p.Items)
            .WithOne()
            .HasForeignKey(i => i.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>
/// EF Core entity configuration for <see cref="PurchaseOrderItem"/>.
/// </summary>
public sealed class PurchaseOrderItemConfiguration : IEntityTypeConfiguration<PurchaseOrderItem>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PurchaseOrderItem> builder)
    {
        builder.ToTable("PurchaseOrderItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(i => i.Quantity)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(i => i.ReceivedQuantity)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(i => i.UnitPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(i => i.TotalAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasIndex(i => i.PurchaseOrderId);
    }
}
