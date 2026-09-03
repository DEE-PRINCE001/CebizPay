using CebizPay.Domain.Communication.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Announcement aggregate.
/// </summary>
public sealed class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.ToTable("Announcements", t =>
        {
            t.HasCheckConstraint(
                "CK_Announcements_Scope_OrganizationId",
                "(\"Scope\" = 1 AND \"OrganizationId\" IS NULL) OR (\"Scope\" = 2 AND \"OrganizationId\" IS NOT NULL)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(x => x.Scope)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.CreatedByUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.PublishedByUserId)
            .HasMaxLength(450);

        builder.Property(x => x.UpdatedByUserId)
            .HasMaxLength(450);

        builder.Property(x => x.ArchivedByUserId)
            .HasMaxLength(450);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.PublishedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.ArchivedAtUtc)
            .HasColumnType("timestamp with time zone");

        // Optimal indexes for Platform and Workplace feed queries
        builder.HasIndex(x => new { x.Scope, x.Status, x.PublishedAtUtc });
        builder.HasIndex(x => new { x.OrganizationId, x.Scope, x.Status, x.PublishedAtUtc });
    }
}
