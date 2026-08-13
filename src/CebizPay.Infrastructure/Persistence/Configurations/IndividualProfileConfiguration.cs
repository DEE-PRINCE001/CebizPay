using CebizPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CebizPay.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for IndividualProfile entity.
/// </summary>
public sealed class IndividualProfileConfiguration : IEntityTypeConfiguration<IndividualProfile>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<IndividualProfile> builder)
    {
        builder.ToTable("IndividualProfiles");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.HasIndex(x => x.UserId)
            .IsUnique();

        builder.Property(x => x.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.MiddleName)
            .HasMaxLength(100);

        builder.Property(x => x.KycStatus)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.ProfessionalStatus)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");
    }
}
