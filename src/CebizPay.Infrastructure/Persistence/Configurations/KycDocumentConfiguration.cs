using CebizPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for KycDocument entity.
/// </summary>
public sealed class KycDocumentConfiguration : IEntityTypeConfiguration<KycDocument>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<KycDocument> builder)
    {
        builder.ToTable("KycDocuments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.DocumentType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.DocumentNumber)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.DocumentUrl)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.RejectionReason)
            .HasMaxLength(500);

        builder.Property(x => x.ReviewedByUserId)
            .HasMaxLength(450);

        builder.Property(x => x.SubmittedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.ReviewedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => x.UserId);
    }
}
