using CebizPay.Domain.Support.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for SupportTicket entity.
/// </summary>
public sealed class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SupportTicket> builder)
    {
        builder.ToTable("SupportTickets");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TicketNumber)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(t => t.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(t => t.Subject)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(t => t.Description)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(t => t.ResolutionSummary)
            .HasMaxLength(2000);

        builder.Property(t => t.IdempotencyKey)
            .HasMaxLength(128);

        builder.Property(t => t.Category)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(t => t.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(t => t.Priority)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(t => t.CreatedAtUtc)
            .IsRequired();

        builder.Property(t => t.UpdatedAtUtc)
            .IsRequired();

        builder.Property(t => t.SlaDueAtUtc)
            .IsRequired();

        builder.Property(t => t.IsSlaBreached)
            .IsRequired();

        // Optimized indexes for tenant/user isolation, SLA monitoring, and administrative filtering
        builder.HasIndex(t => t.TicketNumber)
            .IsUnique();

        builder.HasIndex(t => new { t.UserId, t.CreatedAtUtc });

        builder.HasIndex(t => new { t.OrganizationId, t.CreatedAtUtc });

        builder.HasIndex(t => new { t.Status, t.SlaDueAtUtc });

        builder.HasIndex(t => new { t.Priority, t.Status });

        builder.HasIndex(t => new { t.Category, t.CreatedAtUtc });

        builder.HasIndex(t => new { t.IsSlaBreached, t.SlaDueAtUtc });

        builder.HasIndex(t => new { t.UserId, t.IdempotencyKey });

        builder.HasMany(t => t.Messages)
            .WithOne()
            .HasForeignKey(m => m.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
