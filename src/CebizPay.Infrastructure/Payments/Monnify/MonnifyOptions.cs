using System.ComponentModel.DataAnnotations;

namespace CebizPay.Infrastructure.Payments.Monnify;

/// <summary>
/// Strongly typed configuration options for the Monnify payment / BaaS provider integration.
/// Supports clean enable/disable semantics for local/test/sandbox environments.
/// </summary>
public sealed class MonnifyOptions : IValidatableObject
{
    /// <summary>Configuration section key name.</summary>
    public const string SectionName = "Payments:Monnify";

    /// <summary>Gets or sets whether the Monnify payment provider is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Monnify API Key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Monnify Secret Key.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Monnify Contract Code.</summary>
    public string ContractCode { get; set; } = string.Empty;

    /// <summary>Base URL for Monnify API.</summary>
    public string BaseUrl { get; set; } = "https://api.monnify.com";

    /// <summary>HTTP client request timeout in seconds (defaults to 30).</summary>
    [Range(1, 300, ErrorMessage = "TimeoutSeconds must be between 1 and 300.")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Environment name ("Sandbox" or "Live").</summary>
    public string Environment { get; set; } = "Sandbox";

    /// <summary>Optional dedicated webhook secret or override (defaults to SecretKey if empty).</summary>
    public string? WebhookSecret { get; set; }

    /// <summary>Source wallet/account number for Monnify disbursements/transfers (defaults to ContractCode or main wallet if not set).</summary>
    public string? SourceAccountNumber { get; set; }

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enabled)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            yield return new ValidationResult(
                "ApiKey is required when Monnify provider is enabled.",
                [nameof(ApiKey)]);
        }

        if (string.IsNullOrWhiteSpace(SecretKey))
        {
            yield return new ValidationResult(
                "SecretKey is required when Monnify provider is enabled.",
                [nameof(SecretKey)]);
        }

        if (string.IsNullOrWhiteSpace(ContractCode))
        {
            yield return new ValidationResult(
                "ContractCode is required when Monnify provider is enabled.",
                [nameof(ContractCode)]);
        }

        if (string.IsNullOrWhiteSpace(BaseUrl) || !Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
        {
            yield return new ValidationResult(
                "BaseUrl must be a valid absolute URI.",
                [nameof(BaseUrl)]);
        }
    }
}
