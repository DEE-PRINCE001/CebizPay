using System.Security.Claims;
using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Organizations.Wallet;
using CebizPay.Application.UseCases.Wallet.ExternalAccounts;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Organization corporate wallet management endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/org/wallet")]
[Authorize]
public sealed class OrgWalletController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="OrgWalletController"/>.
    /// </summary>
    public OrgWalletController(ISender sender, ICurrentOrganizationContext orgContext)
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
    /// Retrieves the corporate wallet overview, available balance, and funding account details for the organization.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(OrgWalletOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWallet(CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();

        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.WalletView, cancellationToken))
        {
            return Forbid();
        }

        var query = new GetOrgWalletOverviewQuery(orgId);
        var result = await _sender.Send(query, cancellationToken);

        if (result == null)
        {
            return NotFound(new { code = "WALLET_NOT_FOUND", message = "Corporate wallet not found for this organization." });
        }

        return Ok(new
        {
            success = true,
            data = result
        });
    }

    /// <summary>
    /// Retrieves paginated transaction history for the organization's corporate wallet.
    /// </summary>
    [HttpGet("transactions")]
    [ProducesResponseType(typeof(PagedResult<OrgWalletTransactionItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? type = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();

        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.WalletView, cancellationToken))
        {
            return Forbid();
        }

        var query = new GetOrgWalletTransactionsQuery(orgId, pageNumber, pageSize, search, type, status);
        var result = await _sender.Send(query, cancellationToken);

        return Ok(new
        {
            success = true,
            data = result
        });
    }

    /// <summary>
    /// Retrieves all dedicated virtual accounts allocated to the organization for wallet funding.
    /// </summary>
    [HttpGet("virtual-accounts")]
    [ProducesResponseType(typeof(IReadOnlyList<ExternalFundingAccountResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVirtualAccounts(
        [FromQuery] Currency currency = Currency.NGN,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.WalletView, cancellationToken))
        {
            return Forbid();
        }

        var query = new GetExternalFundingAccountsQuery(userId, orgId, currency);
        var result = await _sender.Send(query, cancellationToken);

        return Ok(new
        {
            success = true,
            data = result
        });
    }
}
