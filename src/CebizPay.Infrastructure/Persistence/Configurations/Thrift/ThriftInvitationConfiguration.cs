using CebizPay.Domain.Thrift.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Thrift;

/// <summary>
/// Entity configuration for ThriftInvitation.
/// </summary>
public class ThriftInvitationConfiguration : IEntityTypeConfiguration<ThriftInvitation>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ThriftInvitation> builder)
    {
        builder.ToTable("ThriftInvitations");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Email)
            .HasMaxLength(256);

        builder.Property(e => e.InvitationCode)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.ExpiresAtUtc)
            .IsRequired();

        builder.Property(e => e.IsAccepted)
            .IsRequired();

        builder.Property(e => e.InvitedByUserId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.AcceptedByUserId)
            .HasMaxLength(256);

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(e => e.InvitationCode)
            .IsUnique();

        builder.HasIndex(e => e.ThriftGroupId);
    }
}
