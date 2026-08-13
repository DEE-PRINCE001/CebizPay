using CebizPay.Domain.Finance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Finance;

/// <summary>
/// EF Core configuration for <see cref="Wallet"/> entity.
/// </summary>
public sealed class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("Wallets", t =>
        {
            t.HasCheckConstraint("CK_Wallets_AvailableBalance_NonNegative", "\"AvailableBalance\" >= 0");
        });

        builder.HasKey(w => w.Id);

        builder.Property(w => w.IndividualId)
            .HasMaxLength(450)
            .IsRequired(false);

        builder.Property(w => w.OrganizationId)
            .IsRequired(false);

        builder.Property(w => w.Currency)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(w => w.AvailableBalance)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(w => w.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(w => w.CreatedAtUtc)
            .IsRequired();

        builder.Property(w => w.UpdatedAtUtc)
            .IsRequired(false);

        // One active wallet per individual per currency
        builder.HasIndex(w => new { w.IndividualId, w.Currency })
            .IsUnique()
            .HasFilter("\"IndividualId\" IS NOT NULL");

        // One active wallet per organization per currency
        builder.HasIndex(w => new { w.OrganizationId, w.Currency })
            .IsUnique()
            .HasFilter("\"OrganizationId\" IS NOT NULL");
    }
}
