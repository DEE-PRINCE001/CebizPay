using CebizPay.Domain.Support.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for TicketMessage entity.
/// </summary>
public sealed class TicketMessageConfiguration : IEntityTypeConfiguration<TicketMessage>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TicketMessage> builder)
    {
        builder.ToTable("TicketMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.TicketId)
            .IsRequired();

        builder.Property(m => m.SenderUserId)
            .HasMaxLength(450);

        builder.Property(m => m.SenderType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(m => m.Content)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(m => m.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(m => new { m.TicketId, m.CreatedAtUtc });
    }
}
