using CebizPay.Domain.Thrift.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for ThriftDispute entity.
/// </summary>
public sealed class ThriftDisputeConfiguration : IEntityTypeConfiguration<ThriftDispute>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ThriftDispute> builder)
    {
        builder.ToTable("ThriftDisputes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ThriftGroupId)
            .IsRequired();

        builder.Property(x => x.ReportedByUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.Reason)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.ResolutionNotes)
            .HasMaxLength(2000);

        builder.Property(x => x.ResolvedByUserId)
            .HasMaxLength(450);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.ResolvedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.ThriftGroupId, x.Status });
        builder.HasIndex(x => x.Status);
    }
}
