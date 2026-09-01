using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Application.Common.Interfaces.Compliance;

/// <summary>
/// Workflow service managing Enhanced Due Diligence (EDD) cases, documentation requests, and decisioning.
/// </summary>
public interface IEddWorkflowService
{
    /// <summary>
    /// Opens a new EDD case triggered by a risk assessment finding.
    /// </summary>
    Task<EddCaseDto> OpenEddCaseAsync(
        RiskSubjectType subjectType,
        string subjectId,
        Guid riskAssessmentId,
        string triggerReason,
        string requiredInformation,
        bool seniorMgmtApprovalRequired = false,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests additional documentation or clarification from the customer.
    /// </summary>
    Task<EddCaseDto> RequestEddInformationAsync(
        Guid eddCaseId,
        string additionalRequirement,
        string adminUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits requested documentation on behalf of the customer.
    /// </summary>
    Task<EddCaseDto> SubmitEddInformationAsync(
        Guid eddCaseId,
        string submittedInformation,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a compliance officer to investigate an EDD case.
    /// </summary>
    Task<EddCaseDto> AssignReviewerAsync(
        Guid eddCaseId,
        string reviewerAdminUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves an EDD case with formal reasoning. Enforces senior management authorization where required.
    /// </summary>
    Task<EddCaseDto> ApproveEddCaseAsync(
        Guid eddCaseId,
        string reason,
        string adminUserId,
        bool isSeniorManagement = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects an EDD case with formal justification.
    /// </summary>
    Task<EddCaseDto> RejectEddCaseAsync(
        Guid eddCaseId,
        string reason,
        string adminUserId,
        CancellationToken cancellationToken = default);
}
