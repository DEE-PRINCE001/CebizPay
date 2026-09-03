namespace CebizPay.Infrastructure.Options;

/// <summary>
/// Configuration options for Firebase Cloud Messaging (FCM).
/// </summary>
public sealed class FirebaseOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Firebase";

    /// <summary>Firebase project identifier.</summary>
    public string? ProjectId { get; set; }

    /// <summary>Raw JSON credentials string or service account JSON file path.</summary>
    public string? CredentialsJson { get; set; }

    /// <summary>Flag indicating whether Firebase push dispatch is active.</summary>
    public bool Enabled { get; set; }
}
