using CebizPay.Domain.Finance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Finance;

/// <summary>
/// EF Core configuration for <see cref="FxConversion"/> entity.
/// </summary>
public sealed class FxConversionConfiguration : IEntityTypeConfiguration<FxConversion>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<FxConversion> builder)
    {
        builder.ToTable("FxConversions", t =>
        {
            t.HasCheckConstraint("CK_FxConversions_SourceAmount_Positive", "\"SourceAmount\" > 0");
            t.HasCheckConstraint("CK_FxConversions_TargetAmount_Positive", "\"TargetAmount\" > 0");
            t.HasCheckConstraint("CK_FxConversions_Rate_Positive", "\"Rate\" > 0");
            t.HasCheckConstraint("CK_FxConversions_Currencies_Different", "\"SourceCurrency\" <> \"TargetCurrency\"");
        });

        builder.HasKey(fx => fx.Id);

        builder.Property(fx => fx.LedgerTransactionId)
            .IsRequired();

        builder.HasIndex(fx => fx.LedgerTransactionId)
            .IsUnique();

        builder.Property(fx => fx.SourceCurrency)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(fx => fx.TargetCurrency)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(fx => fx.SourceAmount)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(fx => fx.TargetAmount)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(fx => fx.Rate)
            .HasPrecision(18, 8)
            .IsRequired();

        builder.Property(fx => fx.RateProvider)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(fx => fx.RateTimestamp)
            .IsRequired();

        builder.Property(fx => fx.CreatedAtUtc)
            .IsRequired();
    }
}
