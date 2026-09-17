using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Organizations.Profile;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Organization corporate profile and verification credentials management controller.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/org/profile")]
[Authorize]
public sealed class OrgProfileController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="OrgProfileController"/>.
    /// </summary>
    public OrgProfileController(ISender sender, ICurrentOrganizationContext orgContext)
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
    /// Retrieves corporate profile, contact details, and KYB credentials for the active tenant organization.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(OrgProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();

        var query = new GetOrgProfileQuery(orgId);
        var result = await _sender.Send(query, cancellationToken);

        if (result == null)
        {
            return NotFound(new { code = "ORGANIZATION_NOT_FOUND", message = "Organization profile not found." });
        }

        return Ok(new
        {
            success = true,
            data = result
        });
    }
}
