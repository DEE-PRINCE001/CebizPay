using System.ComponentModel.DataAnnotations;

namespace CebizPay.Infrastructure.Payments.Flutterwave;

/// <summary>
/// Strongly typed configuration options for the Flutterwave payment provider integration.
/// </summary>
public sealed class FlutterwaveOptions : IValidatableObject
{
    /// <summary>Configuration section key name.</summary>
    public const string SectionName = "Payments:Flutterwave";

    /// <summary>Gets or sets whether the Flutterwave payment provider is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Flutterwave Secret API Key / Token.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Flutterwave Public Key (optional for client-side / tokenization).</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>Flutterwave Client ID (where OAuth / client credential flow is configured).</summary>
    public string? ClientId { get; set; }

    /// <summary>Flutterwave Client Secret (where OAuth / client credential flow is configured).</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Base URL for Flutterwave API (defaults to production or sandbox).</summary>
    public string BaseUrl { get; set; } = "https://api.flutterwave.com";

    /// <summary>HTTP client request timeout in seconds (defaults to 30).</summary>
    [Range(1, 300, ErrorMessage = "TimeoutSeconds must be between 1 and 300.")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Environment name ("Sandbox" or "Live").</summary>
    public string Environment { get; set; } = "Sandbox";

    /// <summary>Secret hash expected in Flutterwave webhook header ("verif-hash").</summary>
    public string WebhookSecretHash { get; set; } = string.Empty;

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enabled)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(SecretKey))
        {
            yield return new ValidationResult(
                "SecretKey is required when Flutterwave provider is enabled.",
                [nameof(SecretKey)]);
        }

        if (string.IsNullOrWhiteSpace(BaseUrl) || !Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
        {
            yield return new ValidationResult(
                "BaseUrl must be a valid absolute URI.",
                [nameof(BaseUrl)]);
        }

        if (TimeoutSeconds <= 0 || TimeoutSeconds > 300)
        {
            yield return new ValidationResult(
                "TimeoutSeconds must be between 1 and 300.",
                [nameof(TimeoutSeconds)]);
        }
    }
}
