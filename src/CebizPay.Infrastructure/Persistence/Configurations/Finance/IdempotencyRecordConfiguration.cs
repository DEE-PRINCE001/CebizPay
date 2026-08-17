using CebizPay.Domain.Finance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Finance;

/// <summary>
/// EF Core configuration for <see cref="IdempotencyRecord"/> entity.
/// </summary>
public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");

        builder.HasKey(ir => ir.Id);

        builder.Property(ir => ir.IdempotencyKey)
            .HasMaxLength(100)
            .IsRequired();

        // Composite unique index for individual actor idempotency
        builder.HasIndex(ir => new { ir.UserId, ir.Operation, ir.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"OrganizationId\" IS NULL AND \"UserId\" IS NOT NULL");

        // Composite unique index for organization actor idempotency
        builder.HasIndex(ir => new { ir.OrganizationId, ir.Operation, ir.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"OrganizationId\" IS NOT NULL");

        builder.Property(ir => ir.UserId)
            .HasMaxLength(450)
            .IsRequired(false);

        builder.Property(ir => ir.OrganizationId)
            .IsRequired(false);

        builder.Property(ir => ir.Operation)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(ir => ir.RequestHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(ir => ir.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(ir => ir.ResponseJson)
            .IsRequired(false);

        builder.Property(ir => ir.CreatedAtUtc)
            .IsRequired();

        builder.Property(ir => ir.CompletedAtUtc)
            .IsRequired(false);
    }
}
