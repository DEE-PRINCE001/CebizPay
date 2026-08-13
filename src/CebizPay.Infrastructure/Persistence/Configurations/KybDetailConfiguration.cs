using CebizPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for KybDetail entity.
/// </summary>
public sealed class KybDetailConfiguration : IEntityTypeConfiguration<KybDetail>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<KybDetail> builder)
    {
        builder.ToTable("KybDetails");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrganizationId)
            .IsRequired();

        builder.Property(x => x.Step)
            .IsRequired();

        builder.Property(x => x.CompanyName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.Phone)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.CacNumber)
            .HasMaxLength(100);

        builder.Property(x => x.LogoUrl)
            .HasMaxLength(1000);

        builder.Property(x => x.CacCertificateUrl)
            .HasMaxLength(1000);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.ReviewedByUserId)
            .HasMaxLength(450);

        builder.Property(x => x.RejectionReason)
            .HasMaxLength(500);

        builder.Property(x => x.SubmittedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.ReviewedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
