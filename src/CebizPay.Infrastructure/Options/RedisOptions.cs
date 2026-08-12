using System.ComponentModel.DataAnnotations;

namespace CebizPay.Infrastructure.Options;

/// <summary>
/// Options for configuring Redis cache connection.
/// </summary>
public sealed class RedisOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Redis";

    /// <summary>
    /// Gets or sets the Redis connection string.
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the key prefix for cache entries.
    /// </summary>
    public string InstanceName { get; set; } = "CebizPay:";
}
