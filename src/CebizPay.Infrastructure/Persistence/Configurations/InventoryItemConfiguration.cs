using CebizPay.Domain.Entities;
using CebizPay.Domain.Erp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for InventoryItem entity.
/// </summary>
public sealed class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("InventoryItems");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrganizationId)
            .IsRequired();

        builder.Property(x => x.Sku)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.Category)
            .HasMaxLength(100);

        builder.Property(x => x.UnitOfMeasure)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Currency)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.CurrentQuantity)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.ReorderLevel)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.CurrentAverageCost)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.SellingPrice)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.IsDeleted)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.DeletedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.OrganizationId, x.Sku })
            .IsUnique();

        builder.HasIndex(x => new { x.OrganizationId, x.Status });
        builder.HasIndex(x => new { x.OrganizationId, x.Name });
        builder.HasIndex(x => new { x.OrganizationId, x.IsDeleted });

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
