using CebizPay.Domain.Payroll.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Payroll;

/// <summary>
/// EF Core configuration for <see cref="PayrollExecutionAttempt"/> entity.
/// </summary>
public sealed class PayrollExecutionAttemptConfiguration : IEntityTypeConfiguration<PayrollExecutionAttempt>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PayrollExecutionAttempt> builder)
    {
        builder.ToTable("PayrollExecutionAttempts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PayrollItemId)
            .IsRequired();

        builder.Property(x => x.AttemptNumber)
            .IsRequired();

        builder.Property(x => x.WorkerId)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.StartedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.CompletedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.FailureCode)
            .HasMaxLength(64);

        builder.Property(x => x.FailureReason)
            .HasMaxLength(1000);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(x => new { x.PayrollItemId, x.AttemptNumber })
            .IsUnique();
    }
}
