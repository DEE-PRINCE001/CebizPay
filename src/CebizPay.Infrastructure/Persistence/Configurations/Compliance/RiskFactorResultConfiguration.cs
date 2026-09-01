#pragma warning disable CS1591
using CebizPay.Domain.Compliance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Compliance;

public sealed class RiskFactorResultConfiguration : IEntityTypeConfiguration<RiskFactorResult>
{
    public void Configure(EntityTypeBuilder<RiskFactorResult> builder)
    {
        builder.ToTable("RiskFactorResults");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.RiskAssessmentId)
            .IsRequired();

        builder.Property(f => f.RuleId)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(f => f.RuleName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(f => f.RiskRating)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(f => f.Reason)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(f => f.EvidenceReference)
            .HasMaxLength(128)
            .IsRequired(false);

        builder.Property(f => f.Severity)
            .IsRequired();

        builder.HasIndex(f => f.RiskAssessmentId);
        builder.HasIndex(f => f.RuleId);
    }
}
