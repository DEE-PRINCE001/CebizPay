namespace CebizPay.Infrastructure.Payments.Flutterwave;

/// <summary>
/// Strongly typed configuration options for the Flutterwave payment provider integration.
/// </summary>
public sealed class FlutterwaveOptions
{
    /// <summary>Configuration section key name.</summary>
    public const string SectionName = "Payments:Flutterwave";

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
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Environment name ("Sandbox" or "Live").</summary>
    public string Environment { get; set; } = "Sandbox";
}
