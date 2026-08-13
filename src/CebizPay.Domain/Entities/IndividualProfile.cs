using CebizPay.Domain.Enums;

namespace CebizPay.Domain.Entities;

/// <summary>
/// Domain representation of an Individual User (natural person).
/// Independent of workplace / staff relationships.
/// </summary>
public class IndividualProfile
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }
    /// <summary>User ID string matching ASP.NET Identity ApplicationUser Id.</summary>
    public string UserId { get; private set; } = string.Empty;
    /// <summary>First name.</summary>
    public string FirstName { get; private set; } = string.Empty;
    /// <summary>Last name.</summary>
    public string LastName { get; private set; } = string.Empty;
    /// <summary>Optional middle name.</summary>
    public string? MiddleName { get; private set; }
    /// <summary>KYC status.</summary>
    public KycStatus KycStatus { get; private set; } = KycStatus.Pending;
    /// <summary>Professional staff status.</summary>
    public ProfessionalStatus ProfessionalStatus { get; private set; } = ProfessionalStatus.NotAStaff;
    /// <summary>Created timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }
    /// <summary>Updated timestamp.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    private IndividualProfile() { } // EF Core

    /// <summary>
    /// Creates a new individual profile.
    /// </summary>
    public IndividualProfile(string userId, string firstName, string lastName, string? middleName = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("FirstName is required.", nameof(firstName));
        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("LastName is required.", nameof(lastName));

        Id = Guid.NewGuid();
        UserId = userId;
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        MiddleName = middleName?.Trim();
        KycStatus = KycStatus.Pending;
        ProfessionalStatus = ProfessionalStatus.NotAStaff;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the KYC status with lifecycle transition validation.
    /// </summary>
    public void SetKycStatus(KycStatus newStatus)
    {
        if (KycStatus == newStatus) return;

        if (KycStatus == KycStatus.Verified && newStatus != KycStatus.Verified)
        {
            throw new InvalidOperationException($"Cannot transition KYC status from {KycStatus} to {newStatus}.");
        }

        KycStatus = newStatus;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the professional status.
    /// </summary>
    public void UpdateProfessionalStatus(ProfessionalStatus status)
    {
        ProfessionalStatus = status;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Returns true if subject to outbound transaction caps.
    /// </summary>
    public bool IsSubjectToTransactionCap() => KycStatus != KycStatus.Verified;

    /// <summary>
    /// Returns true if eligible to accept staff invitation.
    /// </summary>
    public bool CanAcceptStaffInvitation() => KycStatus == KycStatus.Verified;
}
