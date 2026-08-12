namespace CebizPay.Infrastructure.Options;

/// <summary>
/// Options for configuring CORS policies.
/// </summary>
public sealed class CorsOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Cors";

    /// <summary>
    /// Gets or sets the list of allowed CORS origins.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];
}
