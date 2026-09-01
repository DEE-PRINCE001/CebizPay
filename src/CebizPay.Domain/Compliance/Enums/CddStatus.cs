namespace CebizPay.Domain.Compliance.Enums;

/// <summary>
/// Lifecycle status of Customer Due Diligence (CDD) for a subject.
/// </summary>
public enum CddStatus
{
    /// <summary>CDD has not yet been initiated.</summary>
    NotStarted = 1,
    /// <summary>CDD verification and risk evaluation is currently in progress.</summary>
    InProgress = 2,
    /// <summary>CDD has successfully completed with sufficient verifiable evidence.</summary>
    Completed = 3,
    /// <summary>CDD requires Enhanced Due Diligence (EDD) escalation before full clearance.</summary>
    EnhancedRequired = 4,
    /// <summary>CDD requires manual review by a compliance officer due to flagged risks.</summary>
    ReviewRequired = 5,
    /// <summary>CDD has been suspended due to severe compliance flags or sanctions.</summary>
    Suspended = 6
}
