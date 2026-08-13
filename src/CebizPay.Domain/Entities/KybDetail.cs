using CebizPay.Domain.Enums;

namespace CebizPay.Domain.Entities;

/// <summary>
/// Entity capturing 2-step KYB registration &amp; verification metadata.
/// </summary>
public class KybDetail
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }
    /// <summary>Organization ID.</summary>
    public Guid OrganizationId { get; private set; }
    /// <summary>Registration step (1 or 2).</summary>
    public int Step { get; private set; }
    /// <summary>Company name.</summary>
    public string CompanyName { get; private set; } = string.Empty;
    /// <summary>Contact email.</summary>
    public string Email { get; private set; } = string.Empty;
    /// <summary>Contact phone.</summary>
    public string Phone { get; private set; } = string.Empty;
    /// <summary>CAC number.</summary>
    public string? CacNumber { get; private set; }
    /// <summary>Logo URL.</summary>
    public string? LogoUrl { get; private set; }
    /// <summary>CAC certificate URL.</summary>
    public string? CacCertificateUrl { get; private set; }
    /// <summary>KYB verification status.</summary>
    public KybStatus Status { get; private set; } = KybStatus.Pending;
    /// <summary>Reviewer admin user ID.</summary>
    public string? ReviewedByUserId { get; private set; }
    /// <summary>Rejection reason.</summary>
    public string? RejectionReason { get; private set; }
    /// <summary>Submitted timestamp.</summary>
    public DateTime SubmittedAtUtc { get; private set; }
    /// <summary>Reviewed timestamp.</summary>
    public DateTime? ReviewedAtUtc { get; private set; }

    private KybDetail() { } // EF Core

    /// <summary>
    /// Creates a new KYB detail submission record.
    /// </summary>
    public KybDetail(Guid organizationId, int step, string companyName, string email, string phone,
        string? cacNumber = null, string? logoUrl = null, string? cacCertificateUrl = null)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));
        if (step != 1 && step != 2)
            throw new ArgumentException("Step must be 1 or 2.", nameof(step));

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Step = step;
        CompanyName = companyName.Trim();
        Email = email.Trim().ToLowerInvariant();
        Phone = phone.Trim();
        CacNumber = cacNumber?.Trim();
        LogoUrl = logoUrl?.Trim();
        CacCertificateUrl = cacCertificateUrl?.Trim();
        Status = step == 1 ? KybStatus.Step1Completed : KybStatus.Step2Completed;
        SubmittedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Verifies the KYB detail submission.
    /// </summary>
    public void Verify(string adminUserId, DateTime now)
    {
        Status = KybStatus.Verified;
        ReviewedByUserId = adminUserId;
        ReviewedAtUtc = now;
        RejectionReason = null;
    }

    /// <summary>
    /// Rejects the KYB detail submission.
    /// </summary>
    public void Reject(string adminUserId, string reason, DateTime now)
    {
        Status = KybStatus.Rejected;
        ReviewedByUserId = adminUserId;
        ReviewedAtUtc = now;
        RejectionReason = reason;
    }
}
