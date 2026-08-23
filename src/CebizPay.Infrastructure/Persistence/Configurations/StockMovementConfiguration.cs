using CebizPay.Domain.Entities;
using CebizPay.Domain.Erp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for StockMovement entity.
/// </summary>
public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrganizationId)
            .IsRequired();

        builder.Property(x => x.InventoryItemId)
            .IsRequired();

        builder.Property(x => x.MovementType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Quantity)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.UnitCost)
            .HasPrecision(18, 4);

        builder.Property(x => x.TotalCost)
            .HasPrecision(18, 4);

        builder.Property(x => x.Reference)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Reason)
            .HasMaxLength(500);

        builder.Property(x => x.ValuationMethod)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.ValuationPolicyVersion)
            .IsRequired();

        builder.Property(x => x.CreatedByUserId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.InventoryItemId, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.OrganizationId, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.OrganizationId, x.Reference })
            .IsUnique();

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
