using CebizPay.Domain.Erp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core entity configuration for <see cref="OperatingExpense"/>.
/// </summary>
public sealed class OperatingExpenseConfiguration : IEntityTypeConfiguration<OperatingExpense>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<OperatingExpense> builder)
    {
        builder.ToTable("OperatingExpenses");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ExpenseNumber)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(e => e.CreatedByUserId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.ApprovedByUserId)
            .HasMaxLength(100);

        builder.Property(e => e.Reference)
            .HasMaxLength(100);

        builder.HasIndex(e => new { e.OrganizationId, e.ExpenseNumber })
            .IsUnique();

        builder.HasIndex(e => new { e.OrganizationId, e.Category });
        builder.HasIndex(e => new { e.OrganizationId, e.Status });
        builder.HasIndex(e => new { e.OrganizationId, e.ExpenseDate });
    }
}
