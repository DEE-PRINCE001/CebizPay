#pragma warning disable CS1591
using System.ComponentModel.DataAnnotations;

namespace CebizPay.Infrastructure.Compliance.SmileId;

/// <summary>
/// Strongly-typed configuration options for Smile ID compliance and biometric verification gateway.
/// </summary>
public sealed class SmileIdOptions : IValidatableObject
{
    public const string SectionName = "SmileId";

    /// <summary>Whether Smile ID integration is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Base URL for Smile ID REST API (Production or Sandbox).</summary>
    public string BaseUrl { get; set; } = "https://api.smileidentity.com";

    /// <summary>Smile ID Partner ID.</summary>
    public string PartnerId { get; set; } = string.Empty;

    /// <summary>Smile ID API Key for HMAC signature generation.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Server identifier (0 for Test / Sandbox, 1 for Production).</summary>
    public string SidServer { get; set; } = "0";

    /// <summary>Webhook callback secret key.</summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>HTTP client timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enabled)
            yield break;

        if (string.IsNullOrWhiteSpace(BaseUrl) || !Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
            yield return new ValidationResult("BaseUrl must be a valid absolute URI when Smile ID is enabled.", new[] { nameof(BaseUrl) });

        if (string.IsNullOrWhiteSpace(PartnerId))
            yield return new ValidationResult("PartnerId is required when Smile ID is enabled.", new[] { nameof(PartnerId) });

        if (string.IsNullOrWhiteSpace(ApiKey))
            yield return new ValidationResult("ApiKey is required when Smile ID is enabled.", new[] { nameof(ApiKey) });

        if (TimeoutSeconds is <= 0 or > 120)
            yield return new ValidationResult("TimeoutSeconds must be between 1 and 120.", new[] { nameof(TimeoutSeconds) });
    }
}
