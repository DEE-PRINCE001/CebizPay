#pragma warning disable CS1591
using CebizPay.Domain.Compliance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations.Compliance;

public sealed class VerificationEvidenceConfiguration : IEntityTypeConfiguration<VerificationEvidence>
{
    public void Configure(EntityTypeBuilder<VerificationEvidence> builder)
    {
        builder.ToTable("VerificationEvidences");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId)
            .HasMaxLength(450);

        builder.HasIndex(e => e.UserId);

        builder.HasIndex(e => e.OrganizationId);

        builder.Property(e => e.ProviderReference)
            .HasMaxLength(150);

        builder.HasIndex(e => e.ProviderReference);

        builder.Property(e => e.VerificationType)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.Capability)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.Provider)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.ResultStatus)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.ConfidenceScore)
            .HasPrecision(5, 2);

        builder.Property(e => e.FailureCode)
            .HasMaxLength(100);

        builder.Property(e => e.FailureReason)
            .HasMaxLength(500);

        builder.Property(e => e.RawPayloadRef)
            .HasMaxLength(200);
    }
}
