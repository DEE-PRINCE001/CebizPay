using Asp.Versioning;
using CebizPay.Application.UseCases.Admins.GrantPermission;
using CebizPay.Application.UseCases.Admins.RevokePermission;
using CebizPay.Application.UseCases.Individuals.UpdateKycStatus;
using CebizPay.Application.UseCases.Organizations.ReviewKyb;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Admin review, compliance, and permission management endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[Authorize(Policy = CebizPay.Application.Common.Security.AuthorizationPolicies.RequirePlatformAdmin)]
public sealed class AdminReviewController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="AdminReviewController"/>.
    /// </summary>
    public AdminReviewController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Reviews and verifies/rejects an individual's KYC status.
    /// </summary>
    [HttpPost("kyc/review")]
    public async Task<IActionResult> ReviewKyc([FromBody] UpdateKycStatusCommand command, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(command, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Reviews and verifies/rejects an organization's KYB submission.
    /// </summary>
    [HttpPost("kyb/review")]
    public async Task<IActionResult> ReviewKyb([FromBody] ReviewKybCommand command, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(command, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Grants a delegated permission to an admin profile (Super Admin only).
    /// </summary>
    [HttpPost("permissions/grant")]
    [Authorize(Policy = CebizPay.Application.Common.Security.AuthorizationPolicies.RequireSuperAdmin)]
    public async Task<IActionResult> GrantPermission([FromBody] GrantAdminPermissionCommand command, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(command, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Revokes a delegated permission from an admin profile (Super Admin only).
    /// </summary>
    [HttpPost("permissions/revoke")]
    [Authorize(Policy = CebizPay.Application.Common.Security.AuthorizationPolicies.RequireSuperAdmin)]
    public async Task<IActionResult> RevokePermission([FromBody] RevokeAdminPermissionCommand command, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(command, cancellationToken);
        return Ok(response);
    }
}
