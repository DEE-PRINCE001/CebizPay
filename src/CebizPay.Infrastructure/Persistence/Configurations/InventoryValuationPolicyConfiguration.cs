using CebizPay.Domain.Entities;
using CebizPay.Domain.Erp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for InventoryValuationPolicy entity.
/// </summary>
public sealed class InventoryValuationPolicyConfiguration : IEntityTypeConfiguration<InventoryValuationPolicy>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<InventoryValuationPolicy> builder)
    {
        builder.ToTable("InventoryValuationPolicies");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrganizationId)
            .IsRequired();

        builder.Property(x => x.Method)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Version)
            .IsRequired();

        builder.Property(x => x.EffectiveFromUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.DeactivatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedByUserId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.OrganizationId, x.Version })
            .IsUnique();

        builder.HasIndex(x => new { x.OrganizationId, x.IsActive });

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
