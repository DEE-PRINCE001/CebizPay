using CebizPay.Domain.Entities;
using CebizPay.Domain.Erp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Customer entity.
/// </summary>
public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrganizationId)
            .IsRequired();

        builder.Property(x => x.Reference)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Email)
            .HasMaxLength(255);

        builder.Property(x => x.Phone)
            .HasMaxLength(50);

        builder.Property(x => x.Address)
            .HasMaxLength(500);

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

        builder.HasIndex(x => new { x.OrganizationId, x.Reference })
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
