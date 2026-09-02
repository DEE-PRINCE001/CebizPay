namespace CebizPay.Infrastructure.Options;

/// <summary>
/// Strongly typed configuration options for Twilio SMS integration.
/// </summary>
public sealed class TwilioOptions
{
    /// <summary>Configuration section key name in appsettings.json.</summary>
    public const string SectionName = "Twilio";

    /// <summary>Gets or sets whether Twilio SMS delivery is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Twilio Account SID (e.g. ACxxxx...).</summary>
    public string AccountSid { get; set; } = string.Empty;

    /// <summary>Twilio Auth Token.</summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>Twilio Sender Phone Number in E.164 format (e.g. +1234567890).</summary>
    public string FromPhoneNumber { get; set; } = string.Empty;

    /// <summary>Optional Twilio Messaging Service SID (e.g. MGxxxx...).</summary>
    public string? MessagingServiceSid { get; set; }
}
