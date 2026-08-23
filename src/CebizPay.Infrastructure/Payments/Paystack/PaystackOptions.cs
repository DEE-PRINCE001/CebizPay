using System.ComponentModel.DataAnnotations;

namespace CebizPay.Infrastructure.Payments.Paystack;

/// <summary>
/// Strongly typed configuration options for the Paystack payment provider integration.
/// </summary>
public sealed class PaystackOptions : IValidatableObject
{
    /// <summary>Configuration section key name.</summary>
    public const string SectionName = "Payments:Paystack";

    /// <summary>Gets or sets whether the Paystack payment provider is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Paystack Secret API Key.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Paystack Public Key.</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>Base URL for Paystack API.</summary>
    public string BaseUrl { get; set; } = "https://api.paystack.co";

    /// <summary>HTTP client request timeout in seconds (defaults to 30).</summary>
    [Range(1, 300, ErrorMessage = "TimeoutSeconds must be between 1 and 300.")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Environment name ("Sandbox" or "Live").</summary>
    public string Environment { get; set; } = "Sandbox";

    /// <summary>Secret key used for HMAC-SHA512 webhook signature verification (defaults to SecretKey if not specified).</summary>
    public string WebhookSecret { get; set; } = string.Empty;

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
                "SecretKey is required when Paystack provider is enabled.",
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
