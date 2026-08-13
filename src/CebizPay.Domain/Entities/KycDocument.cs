using CebizPay.Domain.Enums;

namespace CebizPay.Domain.Entities;

/// <summary>
/// Domain entity representing a submission of an individual's KYC document.
/// Document types: NIMC, DRIVERS_LICENSE, INTERNATIONAL_PASSPORT, LIVENESS.
/// </summary>
public class KycDocument
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }
    /// <summary>User ID of document owner.</summary>
    public string UserId { get; private set; } = string.Empty;
    /// <summary>Document type.</summary>
    public DocumentType DocumentType { get; private set; }
    /// <summary>Document number/identifier.</summary>
    public string DocumentNumber { get; private set; } = string.Empty;
    /// <summary>Uploaded document URL.</summary>
    public string DocumentUrl { get; private set; } = string.Empty;
    /// <summary>Verification status.</summary>
    public KycStatus Status { get; private set; } = KycStatus.Pending;
    /// <summary>Reason for rejection if rejected.</summary>
    public string? RejectionReason { get; private set; }
    /// <summary>Admin user ID who reviewed the document.</summary>
    public string? ReviewedByUserId { get; private set; }
    /// <summary>Submission timestamp.</summary>
    public DateTime SubmittedAtUtc { get; private set; }
    /// <summary>Review timestamp.</summary>
    public DateTime? ReviewedAtUtc { get; private set; }

    private KycDocument() { } // EF Core

    /// <summary>
    /// Creates a new KYC document record.
    /// </summary>
    public KycDocument(string userId, DocumentType documentType, string documentNumber, string documentUrl)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(documentNumber))
            throw new ArgumentException("DocumentNumber is required.", nameof(documentNumber));
        if (string.IsNullOrWhiteSpace(documentUrl))
            throw new ArgumentException("DocumentUrl is required.", nameof(documentUrl));

        Id = Guid.NewGuid();
        UserId = userId;
        DocumentType = documentType;
        DocumentNumber = documentNumber.Trim();
        DocumentUrl = documentUrl.Trim();
        Status = KycStatus.Pending;
        SubmittedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Approves the KYC document.
    /// </summary>
    public void Approve(string adminUserId, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(adminUserId))
            throw new ArgumentException("AdminUserId is required for review.", nameof(adminUserId));

        Status = KycStatus.Verified;
        ReviewedByUserId = adminUserId;
        ReviewedAtUtc = now;
        RejectionReason = null;
    }

    /// <summary>
    /// Rejects the KYC document.
    /// </summary>
    public void Reject(string adminUserId, string reason, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(adminUserId))
            throw new ArgumentException("AdminUserId is required for review.", nameof(adminUserId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Rejection reason is required.", nameof(reason));

        Status = KycStatus.Rejected;
        ReviewedByUserId = adminUserId;
        ReviewedAtUtc = now;
        RejectionReason = reason.Trim();
    }
}
