using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Dedicated virtual account provisioning and inquiry endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/virtual-accounts")]
[Authorize]
public sealed class VirtualAccountsController : ControllerBase
{
    private readonly IVirtualAccountService _virtualAccountService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualAccountsController"/> class.
    /// </summary>
    public VirtualAccountsController(
        IVirtualAccountService virtualAccountService,
        ICurrentUserService currentUserService,
        ICurrentOrganizationContext orgContext)
    {
        _virtualAccountService = virtualAccountService ?? throw new ArgumentNullException(nameof(virtualAccountService));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
    }

    /// <summary>
    /// Provisions a dedicated virtual account (DVA) for the authenticated individual or active organization.
    /// </summary>
    [HttpPost("provision")]
    public async Task<IActionResult> Provision(
        [FromBody] ProvisionVirtualAccountApiRequest request,
        CancellationToken cancellationToken)
    {
        var currency = request.Currency;
        var provider = request.Provider ?? PaymentProvider.Flutterwave;

        var orgId = _orgContext.CurrentOrganizationId;
        if (orgId.HasValue && orgId.Value != Guid.Empty)
        {
            var orgResult = await _virtualAccountService.ProvisionOrganizationVirtualAccountAsync(
                orgId.Value,
                currency,
                provider,
                cancellationToken).ConfigureAwait(false);

            return Ok(orgResult);
        }

        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "User context missing." });
        }

        var individualResult = await _virtualAccountService.ProvisionIndividualVirtualAccountAsync(
            userId,
            currency,
            provider,
            cancellationToken).ConfigureAwait(false);

        return Ok(individualResult);
    }

    /// <summary>
    /// Retrieves the primary dedicated virtual account for the authenticated user or organization.
    /// </summary>
    [HttpGet("primary")]
    public async Task<IActionResult> GetPrimary(
        [FromQuery] Currency currency = Currency.NGN,
        CancellationToken cancellationToken = default)
    {
        var orgId = _orgContext.CurrentOrganizationId;
        var userId = _currentUserService.UserId;

        var account = await _virtualAccountService.GetVirtualAccountForOwnerAsync(
            individualId: orgId.HasValue && orgId.Value != Guid.Empty ? null : userId,
            organizationId: orgId.HasValue && orgId.Value != Guid.Empty ? orgId.Value : null,
            currency: currency,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (account == null)
        {
            return NotFound(new { code = "VIRTUAL_ACCOUNT_NOT_FOUND", message = $"No active virtual account found for currency '{currency}'." });
        }

        return Ok(account);
    }
}

/// <summary>
/// API request model for provisioning virtual accounts.
/// </summary>
public sealed record ProvisionVirtualAccountApiRequest(
    Currency Currency = Currency.NGN,
    PaymentProvider? Provider = null);
