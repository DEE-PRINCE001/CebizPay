namespace CebizPay.Domain.Entities;

/// <summary>
/// Represents an organization salary level structure.
/// Owned by an Organization for tenant isolation.
/// </summary>
public class SalaryLevel
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }
    /// <summary>Owning organization ID.</summary>
    public Guid OrganizationId { get; private set; }
    /// <summary>Level name.</summary>
    public string LevelName { get; private set; } = string.Empty;
    /// <summary>Base amount.</summary>
    public decimal BaseAmount { get; private set; }
    /// <summary>Currency code (NGN, Int-NGN, USDT).</summary>
    public string Currency { get; private set; } = "NGN";
    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    private SalaryLevel() { } // EF Core

    /// <summary>
    /// Creates a new salary level.
    /// </summary>
    public SalaryLevel(Guid organizationId, string levelName, decimal baseAmount, string currency = "NGN")
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));
        if (string.IsNullOrWhiteSpace(levelName))
            throw new ArgumentException("LevelName is required.", nameof(levelName));
        if (baseAmount < 0)
            throw new ArgumentException("BaseAmount cannot be negative.", nameof(baseAmount));

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        LevelName = levelName.Trim();
        BaseAmount = baseAmount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "NGN" : currency.Trim().ToUpperInvariant();
        CreatedAtUtc = DateTime.UtcNow;
    }
}
