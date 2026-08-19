using Asp.Versioning;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Admin.Audit;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Audit trail querying API endpoints.
/// Supports platform-wide querying for SuperAdmins and tenant-scoped querying for Organization users.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/audit-logs")]
[Authorize]
public sealed class AdminAuditLogsController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="AdminAuditLogsController"/>.
    /// </summary>
    public AdminAuditLogsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Retrieves paginated audit log entries with optional multi-attribute filters.
    /// Requires Permissions.AuditView for platform-wide access or active organization context for tenant-scoped access.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? actorId,
        [FromQuery] string? action,
        [FromQuery] string? resourceType,
        [FromQuery] string? resourceId,
        [FromQuery] Guid? organizationId,
        [FromQuery] string? correlationId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAuditLogsQuery(
            FromUtc: fromUtc,
            ToUtc: toUtc,
            ActorId: actorId,
            Action: action,
            ResourceType: resourceType,
            ResourceId: resourceId,
            OrganizationId: organizationId,
            CorrelationId: correlationId,
            PageNumber: pageNumber,
            PageSize: pageSize);

        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }
}
