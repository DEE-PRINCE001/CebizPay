using System.ComponentModel.DataAnnotations;

namespace CebizPay.Infrastructure.Options;

/// <summary>
/// Strongly typed configuration options for the VTUGATE VAS provider integration.
/// </summary>
public sealed class VtuGateOptions : IValidatableObject
{
    /// <summary>Configuration section key name.</summary>
    public const string SectionName = "VAS:VtuGate";

    /// <summary>Gets or sets whether the VTUGATE VAS provider is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Base URL for the VTUGATE API.</summary>
    public string BaseUrl { get; set; } = "https://vtugate.com/api";

    /// <summary>API key / token used for authenticating with VTUGATE.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>HTTP client request timeout in seconds (defaults to 30).</summary>
    [Range(1, 300, ErrorMessage = "TimeoutSeconds must be between 1 and 300.")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Environment name ("Sandbox" or "Live").</summary>
    public string Environment { get; set; } = "Sandbox";

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
                "ApiKey is required when VtuGate provider is enabled.",
                [nameof(ApiKey)]);
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
