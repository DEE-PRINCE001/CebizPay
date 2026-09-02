using CebizPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for AdminInvitation entity.
/// </summary>
public sealed class AdminInvitationConfiguration : IEntityTypeConfiguration<AdminInvitation>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AdminInvitation> builder)
    {
        builder.ToTable("AdminInvitations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.Role)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.TokenHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.HasIndex(x => x.TokenHash)
            .IsUnique();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.InvitedByUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.RedeemedByUserId)
            .HasMaxLength(450);

        builder.Property(x => x.ExpiresAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.RedeemedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.Email, x.Status, x.ExpiresAtUtc });
    }
}
