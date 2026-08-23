using CebizPay.Domain.Vas.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework Core configuration for <see cref="VasTransaction"/>.
/// </summary>
public sealed class VasTransactionConfiguration : IEntityTypeConfiguration<VasTransaction>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<VasTransaction> builder)
    {
        builder.ToTable("VasTransactions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Reference)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(x => x.Reference)
            .IsUnique();

        builder.Property(x => x.UserId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2);

        builder.Property(x => x.ProductCode)
            .HasMaxLength(64);

        builder.Property(x => x.ProductName)
            .HasMaxLength(128);

        builder.Property(x => x.ProviderReference)
            .HasMaxLength(128);

        builder.Property(x => x.FailureCode)
            .HasMaxLength(64);

        builder.Property(x => x.FailureReason)
            .HasMaxLength(512);

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAtUtc);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.OrganizationId);
    }
}
