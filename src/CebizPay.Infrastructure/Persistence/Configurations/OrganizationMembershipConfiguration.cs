using CebizPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for OrganizationMembership entity.
/// Unique composite index on (UserId, OrganizationId) ensures clean multi-membership logic without duplicate active records.
/// </summary>
public sealed class OrganizationMembershipConfiguration : IEntityTypeConfiguration<OrganizationMembership>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
    {
        builder.ToTable("OrganizationMemberships");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.OrganizationId)
            .IsRequired();

        builder.HasIndex(x => new { x.UserId, x.OrganizationId })
            .IsUnique();

        builder.Property(x => x.Role)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.JoinedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.SuspendedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.SuspensionReason)
            .HasMaxLength(500);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
