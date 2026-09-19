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
    /// <summary>Optional profile avatar/photo URL.</summary>
    public string? AvatarUrl { get; private set; }
    /// <summary>KYC status.</summary>
    public KycStatus KycStatus { get; private set; } = KycStatus.Pending;
    /// <summary>Professional staff status.</summary>
    public ProfessionalStatus ProfessionalStatus { get; private set; } = ProfessionalStatus.NotAStaff;
    /// <summary>Indicates whether the profile is administratively suspended.</summary>
    public bool IsSuspended { get; private set; }
    /// <summary>Timestamp when the profile was suspended.</summary>
    public DateTime? SuspendedAtUtc { get; private set; }
    /// <summary>Regulatory or administrative reason for suspension.</summary>
    public string? SuspensionReason { get; private set; }
    /// <summary>Created timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }
    /// <summary>Updated timestamp.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    private IndividualProfile() { } // EF Core

    /// <summary>
    /// Creates a new individual profile.
    /// </summary>
    public IndividualProfile(string userId, string firstName, string lastName, string? middleName = null, string? avatarUrl = null)
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
        AvatarUrl = avatarUrl?.Trim();
        KycStatus = KycStatus.Pending;
        ProfessionalStatus = ProfessionalStatus.NotAStaff;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the avatar photo URL.
    /// </summary>
    public void UpdateAvatarUrl(string? avatarUrl)
    {
        AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Synchronizes official legal identity details confirmed by national registry (NIBSS/NIMC).
    /// </summary>
    public void SynchronizeLegalIdentity(string firstName, string lastName, string? middleName = null, string? avatarUrl = null)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("Legal first name is required for synchronization.", nameof(firstName));
        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Legal last name is required for synchronization.", nameof(lastName));

        FirstName = firstName.Trim();
        LastName = lastName.Trim();

        if (!string.IsNullOrWhiteSpace(middleName))
            MiddleName = middleName.Trim();

        if (!string.IsNullOrWhiteSpace(avatarUrl) && string.IsNullOrWhiteSpace(AvatarUrl))
            AvatarUrl = avatarUrl.Trim();

        UpdatedAtUtc = DateTime.UtcNow;
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

    /// <summary>
    /// Administratively suspends the individual profile.
    /// </summary>
    public void Suspend(string reason)
    {
        if (IsSuspended)
            throw new InvalidOperationException("Individual profile is already suspended.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Suspension reason is required.", nameof(reason));

        IsSuspended = true;
        SuspensionReason = reason.Trim();
        SuspendedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Administratively reactivates a suspended individual profile.
    /// </summary>
    public void Reactivate()
    {
        if (!IsSuspended)
            throw new InvalidOperationException("Individual profile is not suspended.");

        IsSuspended = false;
        SuspensionReason = null;
        SuspendedAtUtc = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Returns true if allowed to perform outbound financial transactions.
    /// </summary>
    public bool CanTransactOutbound() => !IsSuspended;
}
