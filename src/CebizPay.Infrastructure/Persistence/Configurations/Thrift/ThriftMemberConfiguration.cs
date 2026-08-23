using CebizPay.Domain.Thrift.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Thrift;

/// <summary>
/// Entity configuration for ThriftMember.
/// </summary>
public class ThriftMemberConfiguration : IEntityTypeConfiguration<ThriftMember>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ThriftMember> builder)
    {
        builder.ToTable("ThriftMembers");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.ConsecutiveMissedCycles)
            .IsRequired();

        builder.Property(e => e.TotalContributed)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(e => e.TotalPayoutReceived)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(e => e.JoinedAtUtc)
            .IsRequired();

        // Unique member per thrift group
        builder.HasIndex(e => new { e.ThriftGroupId, e.UserId })
            .IsUnique();

        // Unique position per thrift group (filtered to non-null positions)
        builder.HasIndex(e => new { e.ThriftGroupId, e.Position })
            .IsUnique()
            .HasFilter("\"Position\" IS NOT NULL");
    }
}
