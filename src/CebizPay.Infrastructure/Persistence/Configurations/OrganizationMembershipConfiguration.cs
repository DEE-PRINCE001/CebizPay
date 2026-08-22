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

        builder.Property(x => x.DepartmentId);
        builder.Property(x => x.WorkforceRoleId);
        builder.Property(x => x.SalaryLevelId);

        builder.HasIndex(x => new { x.OrganizationId, x.DepartmentId });
        builder.HasIndex(x => new { x.OrganizationId, x.WorkforceRoleId });
        builder.HasIndex(x => new { x.OrganizationId, x.SalaryLevelId });

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<WorkforceRole>()
            .WithMany()
            .HasForeignKey(x => x.WorkforceRoleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<SalaryLevel>()
            .WithMany()
            .HasForeignKey(x => x.SalaryLevelId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
