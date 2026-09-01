#pragma warning disable CS1591
using CebizPay.Domain.Compliance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Compliance;

public sealed class ComplianceDecisionConfiguration : IEntityTypeConfiguration<ComplianceDecision>
{
    public void Configure(EntityTypeBuilder<ComplianceDecision> builder)
    {
        builder.ToTable("ComplianceDecisions");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.SubjectType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(d => d.SubjectId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(d => d.OrganizationId)
            .IsRequired(false);

        builder.Property(d => d.Decision)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(d => d.RiskRating)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(d => d.CddLevel)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(d => d.EddStatus)
            .HasConversion<int>()
            .IsRequired(false);

        builder.Property(d => d.DecisionReasons)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(d => d.RulesetVersion)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(d => d.DecidedBy)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(d => d.IsManualOverride)
            .IsRequired();

        builder.Property(d => d.OverrideReason)
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.Property(d => d.EffectiveFromUtc)
            .IsRequired();

        builder.Property(d => d.ExpiresAtUtc)
            .IsRequired(false);

        builder.Property(d => d.IsActive)
            .IsRequired();

        builder.HasIndex(d => new { d.SubjectType, d.SubjectId, d.IsActive });
        builder.HasIndex(d => d.OrganizationId);
        builder.HasIndex(d => d.EffectiveFromUtc);
    }
}
