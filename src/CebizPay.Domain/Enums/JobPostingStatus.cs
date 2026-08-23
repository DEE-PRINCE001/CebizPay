namespace CebizPay.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of an organization job posting.
/// </summary>
public enum JobPostingStatus
{
    /// <summary>Draft job posting undergoing editing and not yet visible.</summary>
    Draft = 0,

    /// <summary>Published job posting actively accepting applications.</summary>
    Published = 1,

    /// <summary>Closed job posting no longer accepting new applications.</summary>
    Closed = 2,

    /// <summary>Cancelled job posting.</summary>
    Cancelled = 3
}
