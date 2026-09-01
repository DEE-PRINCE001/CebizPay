#pragma warning disable CS1591
using CebizPay.Domain.Compliance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Compliance;

public sealed class ComplianceRestrictionConfiguration : IEntityTypeConfiguration<ComplianceRestriction>
{
    public void Configure(EntityTypeBuilder<ComplianceRestriction> builder)
    {
        builder.ToTable("ComplianceRestrictions");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.SubjectType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(r => r.SubjectId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(r => r.OrganizationId)
            .IsRequired(false);

        builder.Property(r => r.RestrictionType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(r => r.Reason)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(r => r.DailyCapAmount)
            .HasPrecision(18, 2)
            .IsRequired(false);

        builder.Property(r => r.SingleCapAmount)
            .HasPrecision(18, 2)
            .IsRequired(false);

        builder.Property(r => r.PlacedBy)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(r => r.PlacedAtUtc)
            .IsRequired();

        builder.Property(r => r.IsActive)
            .IsRequired();

        builder.Property(r => r.ReleasedBy)
            .HasMaxLength(128)
            .IsRequired(false);

        builder.Property(r => r.ReleasedAtUtc)
            .IsRequired(false);

        builder.Property(r => r.ReleaseReason)
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.HasIndex(r => new { r.SubjectType, r.SubjectId, r.IsActive });
        builder.HasIndex(r => r.OrganizationId);
        builder.HasIndex(r => r.PlacedAtUtc);
    }
}
