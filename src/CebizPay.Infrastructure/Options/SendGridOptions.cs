namespace CebizPay.Infrastructure.Options;

/// <summary>
/// Strongly typed configuration options for SendGrid email integration.
/// </summary>
public sealed class SendGridOptions
{
    /// <summary>Configuration section key name in appsettings.json.</summary>
    public const string SectionName = "SendGrid";

    /// <summary>Gets or sets whether SendGrid email delivery is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>SendGrid API Key (e.g. SG.xxxx...).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Default sender email address (e.g. noreply@cebizpay.com).</summary>
    public string FromEmail { get; set; } = "noreply@cebizpay.com";

    /// <summary>Default sender display name (e.g. CebizPay).</summary>
    public string FromName { get; set; } = "CebizPay";
}
