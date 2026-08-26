using CebizPay.Domain.Erp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core entity configuration for <see cref="SalesOrder"/>.
/// </summary>
public sealed class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SalesOrder> builder)
    {
        builder.ToTable("SalesOrders");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.OrderNumber)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(s => s.CreatedByUserId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.Notes)
            .HasMaxLength(2000);

        builder.Property(s => s.Subtotal)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.VatAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.TotalAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasIndex(s => new { s.OrganizationId, s.OrderNumber })
            .IsUnique();

        builder.HasIndex(s => new { s.OrganizationId, s.CustomerId });
        builder.HasIndex(s => new { s.OrganizationId, s.Status });

        builder.HasMany(s => s.Items)
            .WithOne()
            .HasForeignKey(i => i.SalesOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>
/// EF Core entity configuration for <see cref="SalesOrderItem"/>.
/// </summary>
public sealed class SalesOrderItemConfiguration : IEntityTypeConfiguration<SalesOrderItem>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SalesOrderItem> builder)
    {
        builder.ToTable("SalesOrderItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(i => i.Quantity)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(i => i.FulfilledQuantity)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(i => i.UnitPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(i => i.TotalAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasIndex(i => i.SalesOrderId);
    }
}
