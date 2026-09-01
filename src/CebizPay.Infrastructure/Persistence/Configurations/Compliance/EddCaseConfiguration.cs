#pragma warning disable CS1591
using CebizPay.Domain.Compliance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Compliance;

public sealed class EddCaseConfiguration : IEntityTypeConfiguration<EddCase>
{
    public void Configure(EntityTypeBuilder<EddCase> builder)
    {
        builder.ToTable("EddCases");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.CaseNumber)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.SubjectType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.SubjectId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(e => e.OrganizationId)
            .IsRequired(false);

        builder.Property(e => e.RiskAssessmentId)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.TriggerReason)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(e => e.RequiredInformation)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(e => e.SubmittedInformation)
            .HasMaxLength(8000)
            .IsRequired(false);

        builder.Property(e => e.AssignedReviewerId)
            .HasMaxLength(128)
            .IsRequired(false);

        builder.Property(e => e.ReviewedByUserId)
            .HasMaxLength(128)
            .IsRequired(false);

        builder.Property(e => e.SeniorManagementApprovalRequired)
            .IsRequired();

        builder.Property(e => e.SeniorManagementApproverId)
            .HasMaxLength(128)
            .IsRequired(false);

        builder.Property(e => e.Decision)
            .HasConversion<int>()
            .IsRequired(false);

        builder.Property(e => e.DecisionReason)
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired();

        builder.Property(e => e.UpdatedAtUtc)
            .IsRequired();

        builder.Property(e => e.CompletedAtUtc)
            .IsRequired(false);

        builder.HasIndex(e => e.CaseNumber).IsUnique();
        builder.HasIndex(e => new { e.SubjectType, e.SubjectId });
        builder.HasIndex(e => e.OrganizationId);
        builder.HasIndex(e => e.Status);
    }
}
