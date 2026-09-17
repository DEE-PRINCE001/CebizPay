namespace CebizPay.Infrastructure.Options;

/// <summary>
/// Top-level configuration options for the Savings module.
/// </summary>
public sealed class SavingsOptions
{
    /// <summary>Configuration section key name.</summary>
    public const string SectionName = "Savings";

    /// <summary>
    /// Active savings provider to route new plans to (e.g. "Mock", "Cowrywise", "Anchor").
    /// Defaults to "Mock" for safe development and testing.
    /// </summary>
    public string ActiveProvider { get; set; } = "Mock";
}
