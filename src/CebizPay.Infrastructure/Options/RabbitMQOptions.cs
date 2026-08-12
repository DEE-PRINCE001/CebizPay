using System.ComponentModel.DataAnnotations;

namespace CebizPay.Infrastructure.Options;

/// <summary>
/// Options for configuring RabbitMQ messaging connection.
/// </summary>
public sealed class RabbitMQOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "RabbitMQ";

    /// <summary>
    /// Gets or sets the RabbitMQ server hostname.
    /// </summary>
    [Required]
    public string HostName { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the RabbitMQ server port.
    /// </summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 5672;

    /// <summary>
    /// Gets or sets the authentication username.
    /// </summary>
    [Required]
    public string UserName { get; set; } = "cebizpay";

    /// <summary>
    /// Gets or sets the authentication password.
    /// </summary>
    [Required]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target virtual host.
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Gets or sets the primary exchange name for events.
    /// </summary>
    public string ExchangeName { get; set; } = "cebizpay.events";
}
