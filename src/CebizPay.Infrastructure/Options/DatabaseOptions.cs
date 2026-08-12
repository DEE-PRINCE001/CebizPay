using System.ComponentModel.DataAnnotations;

namespace CebizPay.Infrastructure.Options;

/// <summary>
/// Options for database connection configuration.
/// </summary>
public sealed class DatabaseOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "ConnectionStrings";

    /// <summary>
    /// Gets or sets the primary PostgreSQL connection string.
    /// </summary>
    [Required]
    public string DefaultConnection { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the command timeout duration in seconds.
    /// </summary>
    [Range(1, 60)]
    public int CommandTimeoutInSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the maximum retry count for transient database failures.
    /// </summary>
    [Range(1, 10)]
    public int MaxRetryCount { get; set; } = 5;
}
