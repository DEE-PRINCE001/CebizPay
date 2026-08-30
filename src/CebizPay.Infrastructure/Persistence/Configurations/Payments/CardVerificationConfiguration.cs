using CebizPay.Domain.Payments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Payments;

/// <summary>
/// EF Core configuration for the <see cref="CardVerification"/> entity.
/// </summary>
public sealed class CardVerificationConfiguration : IEntityTypeConfiguration<CardVerification>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<CardVerification> builder)
    {
        builder.ToTable("CardVerifications");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.UserId)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(v => v.WalletId)
            .IsRequired();

        builder.Property(v => v.Provider)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(v => v.Reference)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(v => v.ProviderReference)
            .HasMaxLength(128);

        builder.Property(v => v.SavedCardId);

        builder.Property(v => v.Amount)
            .IsRequired()
            .HasColumnType("numeric(18,4)")
            .HasDefaultValue(0m);

        builder.Property(v => v.Currency)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(v => v.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(v => v.FailureReason)
            .HasMaxLength(500);

        builder.Property(v => v.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(v => v.CompletedAtUtc)
            .HasColumnType("timestamp with time zone");

        // Indexes
        builder.HasIndex(v => v.Reference)
            .IsUnique()
            .HasDatabaseName("IX_CardVerifications_Reference");

        builder.HasIndex(v => new { v.UserId, v.Status })
            .HasDatabaseName("IX_CardVerifications_UserId_Status");
    }
}
