using CebizPay.Domain.Payments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Payments;

/// <summary>
/// EF Core configuration for the <see cref="SavedCard"/> entity.
/// </summary>
public sealed class SavedCardConfiguration : IEntityTypeConfiguration<SavedCard>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SavedCard> builder)
    {
        builder.ToTable("SavedCards");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.UserId)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(s => s.WalletId)
            .IsRequired();

        builder.Property(s => s.Provider)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(s => s.ProviderCustomerReference)
            .HasMaxLength(128);

        builder.Property(s => s.ProviderToken)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(s => s.Last4)
            .IsRequired()
            .HasMaxLength(4);

        builder.Property(s => s.Brand)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(s => s.ExpiryMonth)
            .HasMaxLength(4);

        builder.Property(s => s.ExpiryYear)
            .HasMaxLength(8);

        builder.Property(s => s.CardHolderName)
            .HasMaxLength(256);

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(s => s.IsDefault)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(s => s.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");

        // Indexes
        // 1. User & Wallet queries
        builder.HasIndex(s => new { s.UserId, s.Status })
            .HasDatabaseName("IX_SavedCards_UserId_Status");

        builder.HasIndex(s => s.WalletId)
            .HasDatabaseName("IX_SavedCards_WalletId");

        // 2. Token uniqueness per provider and user
        builder.HasIndex(s => new { s.UserId, s.Provider, s.ProviderToken })
            .IsUnique()
            .HasDatabaseName("IX_SavedCards_UserId_Provider_ProviderToken");
    }
}
