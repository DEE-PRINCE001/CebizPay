using CebizPay.Domain.Entities;
using CebizPay.Domain.Payroll.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Payroll;

/// <summary>
/// EF Core configuration for <see cref="PayrollBatch"/> aggregate root.
/// </summary>
public sealed class PayrollBatchConfiguration : IEntityTypeConfiguration<PayrollBatch>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PayrollBatch> builder)
    {
        builder.ToTable("PayrollBatches");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BatchReference)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(x => x.BatchReference)
            .IsUnique();

        builder.Property(x => x.OrganizationId)
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.SelectionMode)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.SelectionCriteriaJson)
            .HasColumnType("text");

        builder.Property(x => x.PeriodStart)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.PeriodEnd)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.TotalGrossAmount)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.TotalDeductionsAmount)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.TotalNetAmount)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.CreatedByUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.StartedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.CompletedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.FailedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.FailureReason)
            .HasMaxLength(1000);

        builder.HasIndex(x => new { x.OrganizationId, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.OrganizationId, x.Status });

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.PayrollBatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
