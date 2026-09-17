using System.ComponentModel.DataAnnotations;

namespace CebizPay.Infrastructure.Options;

/// <summary>
/// Configuration options for Cowrywise Embed API integration.
/// </summary>
public sealed class CowrywiseOptions : IValidatableObject
{
    /// <summary>Configuration section key name.</summary>
    public const string SectionName = "Savings:Cowrywise";

    /// <summary>Whether Cowrywise integration is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Base URL for Cowrywise Embed API.</summary>
    public string BaseUrl { get; set; } = "https://sandbox.embed.cowrywise.com/api/v1";

    /// <summary>OAuth2 Client ID for Cowrywise API.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth2 Client Secret for Cowrywise API.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Operating environment ("Sandbox" or "Live").</summary>
    public string Environment { get; set; } = "Sandbox";

    /// <summary>Secret used to verify cryptographic signatures on inbound webhooks.</summary>
    public string? WebhookSecret { get; set; }

    /// <summary>HTTP client request timeout in seconds.</summary>
    [Range(1, 300, ErrorMessage = "TimeoutSeconds must be between 1 and 300.")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enabled)
            yield break;

        if (string.IsNullOrWhiteSpace(BaseUrl) || !Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
            yield return new ValidationResult("BaseUrl must be a valid absolute URI when Cowrywise is enabled.", [nameof(BaseUrl)]);

        if (string.IsNullOrWhiteSpace(ClientId))
            yield return new ValidationResult("ClientId is required when Cowrywise is enabled.", [nameof(ClientId)]);

        if (string.IsNullOrWhiteSpace(ClientSecret))
            yield return new ValidationResult("ClientSecret is required when Cowrywise is enabled.", [nameof(ClientSecret)]);
    }
}
