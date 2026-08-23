namespace CebizPay.Domain.Events;

/// <summary>
/// Domain event published when a draft job posting is created.
/// </summary>
public sealed record JobPostingCreatedDomainEvent(
    Guid JobPostingId,
    Guid OrganizationId,
    string Title,
    string CreatedByUserId,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when a job posting is updated.
/// </summary>
public sealed record JobPostingUpdatedDomainEvent(
    Guid JobPostingId,
    Guid OrganizationId,
    string Title,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when a job posting is published to candidates.
/// </summary>
public sealed record JobPostingPublishedDomainEvent(
    Guid JobPostingId,
    Guid OrganizationId,
    string Title,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when a job posting is closed.
/// </summary>
public sealed record JobPostingClosedDomainEvent(
    Guid JobPostingId,
    Guid OrganizationId,
    string Title,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when a job posting is cancelled.
/// </summary>
public sealed record JobPostingCancelledDomainEvent(
    Guid JobPostingId,
    Guid OrganizationId,
    string Title,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when a candidate application is submitted for a job opening.
/// </summary>
public sealed record RecruitmentApplicationSubmittedDomainEvent(
    Guid ApplicationId,
    Guid JobPostingId,
    Guid OrganizationId,
    string? ApplicantUserId,
    string ApplicantName,
    string ApplicantEmail,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when an application status is moved to under review.
/// </summary>
public sealed record RecruitmentApplicationReviewedDomainEvent(
    Guid ApplicationId,
    Guid JobPostingId,
    Guid OrganizationId,
    string ReviewerUserId,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when an application is shortlisted.
/// </summary>
public sealed record RecruitmentApplicationShortlistedDomainEvent(
    Guid ApplicationId,
    Guid JobPostingId,
    Guid OrganizationId,
    string ReviewerUserId,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when an application is rejected.
/// </summary>
public sealed record RecruitmentApplicationRejectedDomainEvent(
    Guid ApplicationId,
    Guid JobPostingId,
    Guid OrganizationId,
    string ReviewerUserId,
    string RejectionReason,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when an application is accepted.
/// </summary>
public sealed record RecruitmentApplicationAcceptedDomainEvent(
    Guid ApplicationId,
    Guid JobPostingId,
    Guid OrganizationId,
    string ReviewerUserId,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when an application is withdrawn by the candidate.
/// </summary>
public sealed record RecruitmentApplicationWithdrawnDomainEvent(
    Guid ApplicationId,
    Guid JobPostingId,
    Guid OrganizationId,
    DateTime OccurredOnUtc);
