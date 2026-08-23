using CebizPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for JobPosting entity.
/// </summary>
public sealed class JobPostingConfiguration : IEntityTypeConfiguration<JobPosting>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<JobPosting> builder)
    {
        builder.ToTable("JobPostings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrganizationId)
            .IsRequired();

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .IsRequired();

        builder.Property(x => x.EmploymentType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Location)
            .HasMaxLength(150);

        builder.Property(x => x.Requirements)
            .HasMaxLength(4000);

        builder.Property(x => x.Responsibilities)
            .HasMaxLength(4000);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.CreatedByUserId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.ApplicationDeadline)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.PublishedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.ClosedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.OrganizationId, x.Status });
        builder.HasIndex(x => new { x.Status, x.ApplicationDeadline });

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

        builder.HasMany(x => x.Applications)
            .WithOne()
            .HasForeignKey(x => x.JobPostingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
