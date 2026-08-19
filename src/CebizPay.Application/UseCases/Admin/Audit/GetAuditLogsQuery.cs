using CebizPay.Application.Common.Models;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Audit;

/// <summary>
/// Query to search and retrieve paginated audit logs with multi-attribute filtering and tenant isolation.
/// </summary>
public sealed record GetAuditLogsQuery(
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? ActorId = null,
    string? Action = null,
    string? ResourceType = null,
    string? ResourceId = null,
    Guid? OrganizationId = null,
    string? CorrelationId = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<AuditLogDto>>;
