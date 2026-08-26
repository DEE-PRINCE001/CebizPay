namespace CebizPay.Infrastructure.Options;

/// <summary>
/// Options for automated Super Admin initial account seeding.
/// </summary>
public sealed class SuperAdminSeedOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "SuperAdmin";

    /// <summary>
    /// Gets or sets a value indicating whether Super Admin seeding is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the Super Admin email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Super Admin password.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Super Admin phone number.
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Super Admin first name.
    /// </summary>
    public string FirstName { get; set; } = "Super";

    /// <summary>
    /// Gets or sets the Super Admin last name.
    /// </summary>
    public string LastName { get; set; } = "Admin";
}
