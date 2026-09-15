using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Organizations.Staff;
using CebizPay.Domain.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Organization administrators directory endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/org/admins")]
[Authorize]
public sealed class OrgAdminsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="OrgAdminsController"/>.
    /// </summary>
    public OrgAdminsController(ISender sender, ICurrentOrganizationContext orgContext)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
    }

    private Guid GetOrganizationId()
    {
        var orgId = _orgContext.CurrentOrganizationId;
        if (!orgId.HasValue || orgId.Value == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Active organization context header (X-Organization-Id) is required.");
        }
        return orgId.Value;
    }

    /// <summary>
    /// Retrieves all active administrators (Owner and Admin roles) for the organization.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<OrgAdminSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAdmins(CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();

        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.StaffView, cancellationToken))
        {
            return Forbid();
        }

        var query = new GetOrgAdminsQuery(orgId);
        var result = await _sender.Send(query, cancellationToken);

        return Ok(new
        {
            success = true,
            data = result
        });
    }
}
