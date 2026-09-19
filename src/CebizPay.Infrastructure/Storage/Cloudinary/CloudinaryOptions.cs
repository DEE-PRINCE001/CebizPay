#pragma warning disable CS1591
using System.ComponentModel.DataAnnotations;

namespace CebizPay.Infrastructure.Storage.Cloudinary;

/// <summary>
/// Strongly-typed configuration options for Cloudinary media and document storage.
/// Maps to environment variables: Cloudinary__CloudName, Cloudinary__ApiKey, Cloudinary__ApiSecret, etc.
/// </summary>
public sealed class CloudinaryOptions : IValidatableObject
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Cloudinary";

    /// <summary>
    /// Whether Cloudinary integration is active.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Cloudinary cloud name.
    /// </summary>
    public string CloudName { get; set; } = string.Empty;

    /// <summary>
    /// Cloudinary API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Cloudinary API secret.
    /// </summary>
    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>
    /// Root folder name for storing CebizPay assets.
    /// </summary>
    public string Folder { get; set; } = "cebizpay";

    /// <summary>
    /// Maximum allowed file upload size in megabytes.
    /// </summary>
    public int MaxFileSizeMb { get; set; } = 10;

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enabled)
            yield break;

        if (string.IsNullOrWhiteSpace(CloudName))
            yield return new ValidationResult("CloudName is required when Cloudinary is enabled.", new[] { nameof(CloudName) });

        if (string.IsNullOrWhiteSpace(ApiKey))
            yield return new ValidationResult("ApiKey is required when Cloudinary is enabled.", new[] { nameof(ApiKey) });

        if (string.IsNullOrWhiteSpace(ApiSecret))
            yield return new ValidationResult("ApiSecret is required when Cloudinary is enabled.", new[] { nameof(ApiSecret) });

        if (MaxFileSizeMb is <= 0 or > 50)
            yield return new ValidationResult("MaxFileSizeMb must be between 1 and 50 megabytes.", new[] { nameof(MaxFileSizeMb) });
    }
}
