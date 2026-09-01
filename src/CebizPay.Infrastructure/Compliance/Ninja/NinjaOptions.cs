#pragma warning disable CS1591
using System.ComponentModel.DataAnnotations;

namespace CebizPay.Infrastructure.Compliance.Ninja;

/// <summary>
/// Strongly-typed configuration options for Ninja KYC/KYB verification gateway.
/// </summary>
public sealed class NinjaOptions : IValidatableObject
{
    public const string SectionName = "Ninja";

    /// <summary>Whether Ninja integration is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Base URL for Ninja REST API.</summary>
    public string BaseUrl { get; set; } = "https://api.ninjakyc.com";

    /// <summary>Ninja Client ID / Partner ID.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Ninja Client Secret / API Key.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Secret used to verify cryptographic signatures on inbound Ninja webhooks.</summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>HTTP client timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enabled)
            yield break;

        if (string.IsNullOrWhiteSpace(BaseUrl) || !Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
            yield return new ValidationResult("BaseUrl must be a valid absolute URI when Ninja is enabled.", new[] { nameof(BaseUrl) });

        if (string.IsNullOrWhiteSpace(ClientId))
            yield return new ValidationResult("ClientId is required when Ninja is enabled.", new[] { nameof(ClientId) });

        if (string.IsNullOrWhiteSpace(ClientSecret))
            yield return new ValidationResult("ClientSecret is required when Ninja is enabled.", new[] { nameof(ClientSecret) });

        if (TimeoutSeconds is <= 0 or > 120)
            yield return new ValidationResult("TimeoutSeconds must be between 1 and 120.", new[] { nameof(TimeoutSeconds) });
    }
}
