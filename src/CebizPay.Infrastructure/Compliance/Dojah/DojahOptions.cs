#pragma warning disable CS1591
using System.ComponentModel.DataAnnotations;

namespace CebizPay.Infrastructure.Compliance.Dojah;

/// <summary>
/// Strongly-typed configuration options for Dojah identity and business verification API.
/// </summary>
public sealed class DojahOptions : IValidatableObject
{
    public const string SectionName = "Dojah";

    /// <summary>Whether Dojah integration is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Base URL for Dojah REST API.</summary>
    public string BaseUrl { get; set; } = "https://api.dojah.io";

    /// <summary>Dojah App ID / Application identifier.</summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>Dojah Private API Key / Secret Key.</summary>
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>Secret used to verify cryptographic signatures on inbound Dojah webhooks.</summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>HTTP client timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enabled)
            yield break;

        if (string.IsNullOrWhiteSpace(BaseUrl) || !Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
            yield return new ValidationResult("BaseUrl must be a valid absolute URI when Dojah is enabled.", new[] { nameof(BaseUrl) });

        if (string.IsNullOrWhiteSpace(AppId))
            yield return new ValidationResult("AppId is required when Dojah is enabled.", new[] { nameof(AppId) });

        if (string.IsNullOrWhiteSpace(PrivateKey))
            yield return new ValidationResult("PrivateKey is required when Dojah is enabled.", new[] { nameof(PrivateKey) });

        if (TimeoutSeconds is <= 0 or > 120)
            yield return new ValidationResult("TimeoutSeconds must be between 1 and 120.", new[] { nameof(TimeoutSeconds) });
    }
}
