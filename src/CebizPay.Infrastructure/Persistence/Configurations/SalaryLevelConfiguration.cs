using CebizPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for SalaryLevel entity.
/// </summary>
public sealed class SalaryLevelConfiguration : IEntityTypeConfiguration<SalaryLevel>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SalaryLevel> builder)
    {
        builder.ToTable("SalaryLevels");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrganizationId)
            .IsRequired();

        builder.Property(x => x.LevelName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.BaseAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.OrganizationId, x.LevelName })
            .IsUnique();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
