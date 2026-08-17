using CebizPay.Domain.Enums;

namespace CebizPay.Domain.Entities;

/// <summary>
/// Domain aggregate representation of an Organization (B2B tenant).
/// </summary>
public class Organization
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }
    /// <summary>Company name.</summary>
    public string CompanyName { get; private set; } = string.Empty;
    /// <summary>Company contact email.</summary>
    public string Email { get; private set; } = string.Empty;
    /// <summary>Company contact phone.</summary>
    public string Phone { get; private set; } = string.Empty;
    /// <summary>CAC registration number.</summary>
    public string? CacNumber { get; private set; }
    /// <summary>Company logo URL.</summary>
    public string? LogoUrl { get; private set; }
    /// <summary>CAC certificate document URL.</summary>
    public string? CacCertificateUrl { get; private set; }
    /// <summary>Organization lifecycle status.</summary>
    public OrganizationStatus Status { get; private set; } = OrganizationStatus.Pending;
    /// <summary>KYB verification status.</summary>
    public KybStatus KybStatus { get; private set; } = KybStatus.Pending;
    /// <summary>Flag indicating whether details can be edited.</summary>
    public bool CanEditDetails { get; private set; } = true;
    /// <summary>Created timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }
    /// <summary>Updated timestamp.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }
    /// <summary>Soft delete flag.</summary>
    public bool IsDeleted { get; private set; }
    /// <summary>Soft deleted timestamp.</summary>
    public DateTime? DeletedAtUtc { get; private set; }

    private Organization() { } // EF Core

    /// <summary>
    /// Creates a new organization (Step 1 registration).
    /// </summary>
    public Organization(string companyName, string email, string phone)
    {
        if (string.IsNullOrWhiteSpace(companyName))
        {
            throw new ArgumentException("CompanyName is required.", nameof(companyName));
        }
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }
        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new ArgumentException("Phone is required.", nameof(phone));
        }

        Id = Guid.NewGuid();
        CompanyName = companyName.Trim();
        Email = email.Trim().ToLowerInvariant();
        Phone = phone.Trim();
        Status = OrganizationStatus.Pending;
        KybStatus = KybStatus.Step1Completed;
        CanEditDetails = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Completes Step 2 of KYB registration.
    /// </summary>
    public void CompleteStep2(string cacNumber, string logoUrl, string cacCertificateUrl)
    {
        if (string.IsNullOrWhiteSpace(cacNumber))
        {
            throw new ArgumentException("CacNumber is required.", nameof(cacNumber));
        }
        if (string.IsNullOrWhiteSpace(logoUrl))
        {
            throw new ArgumentException("LogoUrl is required.", nameof(logoUrl));
        }
        if (string.IsNullOrWhiteSpace(cacCertificateUrl))
        {
            throw new ArgumentException("CacCertificateUrl is required.", nameof(cacCertificateUrl));
        }

        CacNumber = cacNumber.Trim();
        LogoUrl = logoUrl.Trim();
        CacCertificateUrl = cacCertificateUrl.Trim();
        KybStatus = KybStatus.Step2Completed;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Executes a controlled organization status transition.
    /// </summary>
    public void TransitionStatus(OrganizationStatus newStatus, string? reason = null)
    {
        if (Status == newStatus)
        {
            return;
        }

        var isValid = (Status, newStatus) switch
        {
            (OrganizationStatus.Pending, OrganizationStatus.Verified) => true,
            (OrganizationStatus.Pending, OrganizationStatus.Rejected) => true,
            (OrganizationStatus.Verified, OrganizationStatus.Suspended) => true,
            (OrganizationStatus.Suspended, OrganizationStatus.Verified) => true,
            (OrganizationStatus.Rejected, OrganizationStatus.Pending) => true,
            _ => false
        };

        if (!isValid)
        {
            throw new InvalidOperationException(
                $"Invalid organization status transition from {Status} to {newStatus}.");
        }

        Status = newStatus;
        if (newStatus == OrganizationStatus.Verified)
        {
            KybStatus = KybStatus.Verified;
            CanEditDetails = false;
        }
        else if (newStatus == OrganizationStatus.Rejected)
        {
            KybStatus = KybStatus.Rejected;
            CanEditDetails = true;
        }
        else if (newStatus == OrganizationStatus.Suspended)
        {
            KybStatus = KybStatus.Suspended;
        }

        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets the KYB status of the organization directly.
    /// </summary>
    public void SetKybStatus(KybStatus kybStatus)
    {
        KybStatus = kybStatus;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Performs soft delete of the organization.
    /// </summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Section 10 Rule: Automated payroll requires Organization Status == VERIFIED and KYB Status == VERIFIED.
    /// </summary>
    public bool CanExecutePayroll() => Status == OrganizationStatus.Verified && KybStatus == KybStatus.Verified;

    /// <summary>
    /// Section 10 Rule: Corporate wallet transfers require Organization Status == VERIFIED and KYB Status == VERIFIED.
    /// </summary>
    public bool CanExecuteWalletTransfers() => Status == OrganizationStatus.Verified && KybStatus == KybStatus.Verified;

    /// <summary>
    /// Section 10 Rule: Configuring HRIS/hierarchy is allowed in PENDING or REJECTED state (not SUSPENDED).
    /// </summary>
    public bool CanConfigureHris() => Status != OrganizationStatus.Suspended;
}
