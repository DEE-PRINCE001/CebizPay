using CebizPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for RecruitmentApplication entity.
/// </summary>
public sealed class RecruitmentApplicationConfiguration : IEntityTypeConfiguration<RecruitmentApplication>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RecruitmentApplication> builder)
    {
        builder.ToTable("RecruitmentApplications");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.JobPostingId)
            .IsRequired();

        builder.Property(x => x.OrganizationId)
            .IsRequired();

        builder.Property(x => x.ApplicantUserId)
            .HasMaxLength(100);

        builder.Property(x => x.ApplicantName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.ApplicantEmail)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.ApplicantPhone)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ResumeReference)
            .HasMaxLength(1000);

        builder.Property(x => x.CoverLetter)
            .HasMaxLength(4000);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.ReviewedByUserId)
            .HasMaxLength(100);

        builder.Property(x => x.RejectionReason)
            .HasMaxLength(1000);

        builder.Property(x => x.ReviewNotes)
            .HasMaxLength(2000);

        builder.Property(x => x.AppliedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.ReviewedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.JobPostingId, x.Status });
        builder.HasIndex(x => new { x.OrganizationId, x.Status });
        builder.HasIndex(x => x.ApplicantUserId);
        builder.HasIndex(x => new { x.JobPostingId, x.ApplicantEmail });

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
