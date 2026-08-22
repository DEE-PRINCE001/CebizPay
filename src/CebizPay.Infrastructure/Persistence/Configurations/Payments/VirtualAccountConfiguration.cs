using CebizPay.Domain.Payments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Payments;

/// <summary>
/// EF Core configuration for the <see cref="VirtualAccount"/> entity.
/// </summary>
public sealed class VirtualAccountConfiguration : IEntityTypeConfiguration<VirtualAccount>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<VirtualAccount> builder)
    {
        builder.ToTable("VirtualAccounts");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.IndividualId)
            .HasMaxLength(128);

        builder.Property(v => v.OrganizationId);

        builder.Property(v => v.Provider)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(v => v.AccountNumber)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(v => v.AccountName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(v => v.BankCode)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(v => v.BankName)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(v => v.Currency)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(v => v.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(v => v.ProviderReference)
            .HasMaxLength(128);

        builder.Property(v => v.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(v => v.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");

        // Indexes
        // 1. Account number unique per provider
        builder.HasIndex(v => new { v.Provider, v.AccountNumber })
            .IsUnique()
            .HasDatabaseName("IX_VirtualAccounts_Provider_AccountNumber");

        // 2. Single primary account per individual + provider + currency
        builder.HasIndex(v => new { v.IndividualId, v.Provider, v.Currency })
            .IsUnique()
            .HasFilter("\"IndividualId\" IS NOT NULL")
            .HasDatabaseName("IX_VirtualAccounts_IndividualId_Provider_Currency");

        // 3. Single primary account per organization + provider + currency
        builder.HasIndex(v => new { v.OrganizationId, v.Provider, v.Currency })
            .IsUnique()
            .HasFilter("\"OrganizationId\" IS NOT NULL")
            .HasDatabaseName("IX_VirtualAccounts_OrganizationId_Provider_Currency");

        // 4. Query indexes
        builder.HasIndex(v => v.Status)
            .HasDatabaseName("IX_VirtualAccounts_Status");
    }
}
