#pragma warning disable CS1591
using CebizPay.Domain.Compliance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Compliance;

public sealed class RiskAssessmentConfiguration : IEntityTypeConfiguration<RiskAssessment>
{
    public void Configure(EntityTypeBuilder<RiskAssessment> builder)
    {
        builder.ToTable("RiskAssessments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.SubjectType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(a => a.SubjectId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(a => a.OrganizationId)
            .IsRequired(false);

        builder.Property(a => a.RiskRating)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(a => a.CddLevel)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(a => a.EddRequired)
            .IsRequired();

        builder.Property(a => a.RulesetVersion)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(a => a.Summary)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(a => a.EvaluatedAtUtc)
            .IsRequired();

        builder.Property(a => a.ExpiresAtUtc)
            .IsRequired(false);

        builder.Property(a => a.IsCurrent)
            .IsRequired();

        builder.HasMany(a => a.RiskFactors)
            .WithOne()
            .HasForeignKey(f => f.RiskAssessmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.SubjectType, a.SubjectId, a.IsCurrent });
        builder.HasIndex(a => a.OrganizationId);
        builder.HasIndex(a => a.EvaluatedAtUtc);
    }
}
