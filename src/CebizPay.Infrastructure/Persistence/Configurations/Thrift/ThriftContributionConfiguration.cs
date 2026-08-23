using CebizPay.Domain.Thrift.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Thrift;

/// <summary>
/// Entity configuration for ThriftContribution.
/// </summary>
public class ThriftContributionConfiguration : IEntityTypeConfiguration<ThriftContribution>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ThriftContribution> builder)
    {
        builder.ToTable("ThriftContributions");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(e => e.Currency)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.Source)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.IdempotencyKey)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(e => e.FailureReason)
            .HasMaxLength(1000);

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired();

        // Unique member contribution per cycle
        builder.HasIndex(e => new { e.ThriftCycleId, e.MemberId })
            .IsUnique();

        builder.HasIndex(e => e.ThriftGroupId);
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.IdempotencyKey);
    }
}
