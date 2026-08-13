using CebizPay.Domain.Finance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Finance;

/// <summary>
/// EF Core configuration for <see cref="LedgerTransaction"/> entity.
/// </summary>
public sealed class LedgerTransactionConfiguration : IEntityTypeConfiguration<LedgerTransaction>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<LedgerTransaction> builder)
    {
        builder.ToTable("LedgerTransactions");

        builder.HasKey(lt => lt.Id);

        builder.Property(lt => lt.Reference)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(lt => lt.Reference)
            .IsUnique();

        builder.Property(lt => lt.TransactionType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(lt => lt.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(lt => lt.IdempotencyKey)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(lt => lt.Description)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(lt => lt.CreatedAtUtc)
            .IsRequired();

        builder.Property(lt => lt.CompletedAtUtc)
            .IsRequired(false);

        builder.HasIndex(lt => lt.IdempotencyKey)
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");
    }
}
