namespace CebizPay.Domain.Enums;

/// <summary>
/// Represents the candidate application review lifecycle status.
/// </summary>
public enum ApplicationStatus
{
    /// <summary>Application submitted and awaiting initial review.</summary>
    Submitted = 0,

    /// <summary>Application currently being reviewed by hiring manager / HR.</summary>
    UnderReview = 1,

    /// <summary>Candidate application shortlisted for interviews / offer.</summary>
    Shortlisted = 2,

    /// <summary>Application rejected.</summary>
    Rejected = 3,

    /// <summary>Candidate accepted / offer extended.</summary>
    Accepted = 4,

    /// <summary>Application voluntarily withdrawn by candidate.</summary>
    Withdrawn = 5
}
