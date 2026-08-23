using CebizPay.Domain.Thrift.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Thrift;

/// <summary>
/// Entity configuration for ThriftCycle.
/// </summary>
public class ThriftCycleConfiguration : IEntityTypeConfiguration<ThriftCycle>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ThriftCycle> builder)
    {
        builder.ToTable("ThriftCycles");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.CycleNumber)
            .IsRequired();

        builder.Property(e => e.StartDateUtc)
            .IsRequired();

        builder.Property(e => e.EndDateUtc)
            .IsRequired();

        builder.Property(e => e.DueDateUtc)
            .IsRequired();

        builder.Property(e => e.TargetPayoutPosition)
            .IsRequired();

        builder.Property(e => e.TargetBeneficiaryUserId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.TotalExpectedPool)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(e => e.TotalCollectedPool)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.FailureReason)
            .HasMaxLength(1000);

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired();

        builder.HasMany(e => e.Contributions)
            .WithOne()
            .HasForeignKey(c => c.ThriftCycleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.Contributions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(e => new { e.ThriftGroupId, e.CycleNumber })
            .IsUnique();

        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.DueDateUtc);
    }
}
