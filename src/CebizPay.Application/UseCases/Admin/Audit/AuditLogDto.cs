namespace CebizPay.Application.UseCases.Admin.Audit;

/// <summary>
/// Data Transfer Object representing an immutable audit log entry.
/// </summary>
public sealed record AuditLogDto(
    Guid Id,
    string ActorId,
    Guid? OrganizationId,
    string Action,
    string ResourceType,
    string? ResourceId,
    string? BeforeJson,
    string? AfterJson,
    string? IpAddress,
    string? UserAgent,
    string? CorrelationId,
    DateTime OccurredAtUtc);
