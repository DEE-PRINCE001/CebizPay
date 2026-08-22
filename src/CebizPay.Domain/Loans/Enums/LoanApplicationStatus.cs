namespace CebizPay.Domain.Loans.Enums;

/// <summary>
/// Lifecycle status of a corporate staff loan application.
/// </summary>
public enum LoanApplicationStatus
{
    /// <summary>Draft application not yet submitted for review.</summary>
    Draft = 1,
    /// <summary>Application submitted by staff member and awaiting review/decision.</summary>
    Submitted = 2,
    /// <summary>Application routed for manual HR / underwriting review (e.g. salary verification).</summary>
    UnderReview = 3,
    /// <summary>Application approved by authorized organization executive.</summary>
    Approved = 4,
    /// <summary>Application formally declined.</summary>
    Declined = 5,
    /// <summary>Application cancelled by applicant prior to approval.</summary>
    Cancelled = 6
}
