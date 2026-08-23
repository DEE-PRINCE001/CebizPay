namespace CebizPay.Domain.Thrift.Entities;

/// <summary>
/// Domain entity representing an invitation issued to join a Thrift group.
/// Supports both targeted email invitations and secure shareable invitation codes.
/// </summary>
public class ThriftInvitation
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Parent Thrift group ID.</summary>
    public Guid ThriftGroupId { get; private set; }

    /// <summary>Optional recipient email address.</summary>
    public string? Email { get; private set; }

    /// <summary>Secure random invitation code.</summary>
    public string InvitationCode { get; private set; } = string.Empty;

    /// <summary>Invitation expiration timestamp.</summary>
    public DateTime ExpiresAtUtc { get; private set; }

    /// <summary>Indicates whether the invitation has been accepted.</summary>
    public bool IsAccepted { get; private set; }

    /// <summary>Identity user ID of the inviting member.</summary>
    public string InvitedByUserId { get; private set; } = string.Empty;

    /// <summary>Identity user ID of the user who accepted the invitation.</summary>
    public string? AcceptedByUserId { get; private set; }

    /// <summary>Timestamp when invitation was accepted.</summary>
    public DateTime? AcceptedAtUtc { get; private set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    private ThriftInvitation() { } // EF Core

    /// <summary>
    /// Creates a new thrift invitation.
    /// </summary>
    public static ThriftInvitation Create(
        Guid thriftGroupId,
        string? email,
        string invitedByUserId,
        TimeSpan validityPeriod)
    {
        if (thriftGroupId == Guid.Empty)
            throw new ArgumentException("ThriftGroupId is required.", nameof(thriftGroupId));
        if (string.IsNullOrWhiteSpace(invitedByUserId))
            throw new ArgumentException("InvitedByUserId is required.", nameof(invitedByUserId));

        var code = $"THRIFT-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        return new ThriftInvitation
        {
            Id = Guid.NewGuid(),
            ThriftGroupId = thriftGroupId,
            Email = email?.Trim().ToLowerInvariant(),
            InvitationCode = code,
            ExpiresAtUtc = DateTime.UtcNow.Add(validityPeriod),
            IsAccepted = false,
            InvitedByUserId = invitedByUserId,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Accepts the invitation on behalf of an authenticated user.
    /// </summary>
    public void Accept(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (IsAccepted)
            throw new InvalidOperationException("Invitation has already been accepted.");
        if (DateTime.UtcNow > ExpiresAtUtc)
            throw new InvalidOperationException("Invitation has expired.");

        IsAccepted = true;
        AcceptedByUserId = userId;
        AcceptedAtUtc = DateTime.UtcNow;
    }
}
