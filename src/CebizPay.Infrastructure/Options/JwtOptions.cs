using System.ComponentModel.DataAnnotations;

namespace CebizPay.Infrastructure.Options;

/// <summary>
/// Options for configuring JWT authentication.
/// </summary>
public sealed class JwtOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Jwt";

    /// <summary>
    /// Gets or sets the JWT secret key used for signing tokens.
    /// </summary>
    [Required]
    [MinLength(32, ErrorMessage = "JWT Signing key must be at least 256 bits (32 characters) long.")]
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the valid token issuer.
    /// </summary>
    [Required]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the valid token audience.
    /// </summary>
    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the token expiration time in minutes.
    /// </summary>
    [Range(1, 1440)]
    public int ExpirationInMinutes { get; set; } = 60;
}
