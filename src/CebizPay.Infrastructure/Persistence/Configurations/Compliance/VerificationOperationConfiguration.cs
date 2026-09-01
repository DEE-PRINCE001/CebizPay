#pragma warning disable CS1591
using CebizPay.Domain.Compliance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Compliance;

public sealed class VerificationOperationConfiguration : IEntityTypeConfiguration<VerificationOperation>
{
    public void Configure(EntityTypeBuilder<VerificationOperation> builder)
    {
        builder.ToTable("VerificationOperations");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Reference)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(o => o.Reference)
            .IsUnique();

        builder.Property(o => o.UserId)
            .HasMaxLength(450);

        builder.HasIndex(o => o.UserId);

        builder.HasIndex(o => o.OrganizationId);

        builder.Property(o => o.IdempotencyKey)
            .HasMaxLength(100);

        builder.HasIndex(o => o.IdempotencyKey);

        builder.Property(o => o.FailureReason)
            .HasMaxLength(500);

        builder.Property(o => o.VerificationType)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(o => o.Capability)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(o => o.PrimaryProvider)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(o => o.ActiveProvider)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasMany(o => o.Evidences)
            .WithOne()
            .HasForeignKey(e => e.VerificationOperationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
