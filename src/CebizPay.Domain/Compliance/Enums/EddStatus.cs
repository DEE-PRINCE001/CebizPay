namespace CebizPay.Domain.Compliance.Enums;

/// <summary>
/// Lifecycle status of an Enhanced Due Diligence (EDD) case.
/// </summary>
public enum EddStatus
{
    /// <summary>EDD has been triggered by risk rules and is required.</summary>
    Required = 1,
    /// <summary>EDD case has been opened and assigned for active investigation.</summary>
    Initiated = 2,
    /// <summary>Additional documentation or information has been requested from the customer.</summary>
    InformationRequested = 3,
    /// <summary>Customer has submitted the requested EDD documentation.</summary>
    InformationSubmitted = 4,
    /// <summary>Submitted documentation is actively being evaluated by a compliance officer.</summary>
    InReview = 5,
    /// <summary>EDD case has been approved by authorized compliance officer / senior management.</summary>
    Approved = 6,
    /// <summary>EDD case has been rejected due to inadequate information or prohibitive risk.</summary>
    Rejected = 7,
    /// <summary>EDD case has been escalated for senior executive or board-level review.</summary>
    Escalated = 8
}
