using System.ComponentModel.DataAnnotations;

namespace CebizPay.Infrastructure.Options;

/// <summary>
/// Configuration options for Anchor BaaS API integration.
/// </summary>
public sealed class AnchorOptions : IValidatableObject
{
    /// <summary>Configuration section key name.</summary>
    public const string SectionName = "Savings:Anchor";

    /// <summary>Whether Anchor integration is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Base URL for Anchor REST API.</summary>
    public string BaseUrl { get; set; } = "https://api.sandbox.getanchor.co/api/v1";

    /// <summary>Anchor API Key sent via x-anchor-key header.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>ID of the root FBO deposit account under which customer sub-accounts are opened.</summary>
    public string ParentFboAccountId { get; set; } = string.Empty;

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
            yield return new ValidationResult("BaseUrl must be a valid absolute URI when Anchor is enabled.", [nameof(BaseUrl)]);

        if (string.IsNullOrWhiteSpace(ApiKey))
            yield return new ValidationResult("ApiKey is required when Anchor is enabled.", [nameof(ApiKey)]);

        if (string.IsNullOrWhiteSpace(ParentFboAccountId))
            yield return new ValidationResult("ParentFboAccountId is required when Anchor is enabled.", [nameof(ParentFboAccountId)]);
    }
}
