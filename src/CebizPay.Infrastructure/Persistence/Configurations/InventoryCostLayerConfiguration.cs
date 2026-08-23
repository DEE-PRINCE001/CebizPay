using CebizPay.Domain.Entities;
using CebizPay.Domain.Erp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for InventoryCostLayer entity.
/// </summary>
public sealed class InventoryCostLayerConfiguration : IEntityTypeConfiguration<InventoryCostLayer>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<InventoryCostLayer> builder)
    {
        builder.ToTable("InventoryCostLayers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrganizationId)
            .IsRequired();

        builder.Property(x => x.InventoryItemId)
            .IsRequired();

        builder.Property(x => x.SourceMovementId)
            .IsRequired();

        builder.Property(x => x.OriginalQuantity)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.RemainingQuantity)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.UnitCost)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.InventoryItemId, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.InventoryItemId, x.RemainingQuantity });
        builder.HasIndex(x => new { x.OrganizationId, x.InventoryItemId });

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<InventoryItem>()
            .WithMany()
            .HasForeignKey(x => x.InventoryItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
