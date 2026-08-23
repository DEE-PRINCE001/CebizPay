using CebizPay.Domain.Thrift.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Thrift;

/// <summary>
/// Entity configuration for ThriftGroup.
/// </summary>
public class ThriftGroupConfiguration : IEntityTypeConfiguration<ThriftGroup>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ThriftGroup> builder)
    {
        builder.ToTable("ThriftGroups");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.CreatorUserId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(1000);

        builder.Property(e => e.Currency)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.ContributionAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(e => e.Frequency)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.TotalPositions)
            .IsRequired();

        builder.Property(e => e.StartDateUtc)
            .IsRequired();

        builder.Property(e => e.EndDateUtc)
            .IsRequired();

        builder.Property(e => e.PositionSelectionDeadlineUtc)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.CurrentCycleNumber)
            .IsRequired();

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired();

        builder.HasMany(e => e.Members)
            .WithOne()
            .HasForeignKey(m => m.ThriftGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.Members)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(e => e.Invitations)
            .WithOne()
            .HasForeignKey(i => i.ThriftGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.Invitations)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(e => e.Cycles)
            .WithOne()
            .HasForeignKey(c => c.ThriftGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.Cycles)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(e => e.OrganizationId);
        builder.HasIndex(e => e.CreatorUserId);
        builder.HasIndex(e => e.Status);
    }
}
