using CebizPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the immutable AuditLog entity.
/// </summary>
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActorId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(x => x.OrganizationId)
            .IsRequired(false);

        builder.Property(x => x.Action)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.ResourceType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.ResourceId)
            .HasMaxLength(450)
            .IsRequired(false);

        builder.Property(x => x.BeforeJson)
            .IsRequired(false);

        builder.Property(x => x.AfterJson)
            .IsRequired(false);

        builder.Property(x => x.IpAddress)
            .HasMaxLength(45)
            .IsRequired(false);

        builder.Property(x => x.UserAgent)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(x => x.CorrelationId)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(x => x.OccurredAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Query performance indexes
        builder.HasIndex(x => x.OccurredAtUtc);
        builder.HasIndex(x => x.ActorId);
        builder.HasIndex(x => x.OrganizationId);
        builder.HasIndex(x => x.Action);
        builder.HasIndex(x => x.CorrelationId);
        builder.HasIndex(x => new { x.ResourceType, x.ResourceId });
    }
}
