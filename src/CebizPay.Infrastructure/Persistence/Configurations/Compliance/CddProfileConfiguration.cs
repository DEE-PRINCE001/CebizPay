#pragma warning disable CS1591
using CebizPay.Domain.Compliance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Compliance;

public sealed class CddProfileConfiguration : IEntityTypeConfiguration<CddProfile>
{
    public void Configure(EntityTypeBuilder<CddProfile> builder)
    {
        builder.ToTable("CddProfiles");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.SubjectType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(c => c.SubjectId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(c => c.OrganizationId)
            .IsRequired(false);

        builder.Property(c => c.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(c => c.RiskRating)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(c => c.CddLevel)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(c => c.Tier)
            .IsRequired(false);

        builder.Property(c => c.LatestRiskAssessmentId)
            .IsRequired(false);

        builder.Property(c => c.CompletedAtUtc)
            .IsRequired(false);

        builder.Property(c => c.LastEvaluatedAtUtc)
            .IsRequired();

        builder.Property(c => c.ReviewNotes)
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.HasIndex(c => new { c.SubjectType, c.SubjectId }).IsUnique();
        builder.HasIndex(c => c.OrganizationId);
        builder.HasIndex(c => c.Status);
    }
}
