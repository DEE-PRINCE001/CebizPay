using CebizPay.Domain.Entities;
using CebizPay.Domain.Erp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for ErpService entity.
/// </summary>
public sealed class ErpServiceConfiguration : IEntityTypeConfiguration<ErpService>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ErpService> builder)
    {
        builder.ToTable("ErpServices");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrganizationId)
            .IsRequired();

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.UnitPrice)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasConversion<int>()
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

        builder.HasIndex(x => new { x.OrganizationId, x.Code })
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
