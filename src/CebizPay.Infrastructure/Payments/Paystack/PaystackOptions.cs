namespace CebizPay.Infrastructure.Payments.Paystack;

/// <summary>
/// Strongly typed configuration options for the Paystack payment provider integration.
/// </summary>
public sealed class PaystackOptions
{
    /// <summary>Configuration section key name.</summary>
    public const string SectionName = "Payments:Paystack";

    /// <summary>Paystack Secret API Key.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Paystack Public Key.</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>Base URL for Paystack API.</summary>
    public string BaseUrl { get; set; } = "https://api.paystack.co";

    /// <summary>HTTP client request timeout in seconds (defaults to 30).</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Environment name ("Sandbox" or "Live").</summary>
    public string Environment { get; set; } = "Sandbox";

    /// <summary>Secret key used for HMAC-SHA512 webhook signature verification (defaults to SecretKey if not specified).</summary>
    public string WebhookSecret { get; set; } = string.Empty;
}
