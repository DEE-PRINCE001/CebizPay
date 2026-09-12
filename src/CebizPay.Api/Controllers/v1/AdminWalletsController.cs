using Asp.Versioning;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Admin.Wallets;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Platform Administrative Wallet Directory and Liquidity Management Controller.
/// Enables platform administrators (SuperAdmin, Admin, Auditor) to inspect and export corporate and individual wallet balances, salary totals, and loan obligations.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/wallets")]
[Authorize(Roles = "SuperAdmin,Admin,Auditor")]
public sealed class AdminWalletsController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="AdminWalletsController"/>.
    /// </summary>
    public AdminWalletsController(ISender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    /// <summary>
    /// Retrieves a paginated directory of all corporate organization wallets with balances, cumulative salary disbursements, and total loan disbursements across the platform.
    /// </summary>
    [HttpGet("organizations")]
    [ProducesResponseType(typeof(PagedResult<AdminOrganizationWalletSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOrganizationWalletsDirectory(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAdminOrganizationWalletsDirectoryQuery(pageNumber, pageSize, search, status);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Exports the corporate organization wallets directory list directly to a downloadable CSV stream.
    /// </summary>
    [HttpGet("organizations/export")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportOrganizationWallets(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string format = "csv",
        CancellationToken cancellationToken = default)
    {
        var query = new ExportAdminOrganizationWalletsQuery(search, status, format);
        var result = await _sender.Send(query, cancellationToken);
        return File(result.Content, result.ContentType, result.FileName);
    }

    /// <summary>
    /// Retrieves a paginated directory of all individual user wallets with current available balances and total outstanding repayable loan obligations.
    /// </summary>
    [HttpGet("individuals")]
    [ProducesResponseType(typeof(PagedResult<AdminIndividualWalletSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetIndividualWalletsDirectory(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAdminIndividualWalletsDirectoryQuery(pageNumber, pageSize, search, status);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Exports the individual user wallets directory list directly to a downloadable CSV stream.
    /// </summary>
    [HttpGet("individuals/export")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportIndividualWallets(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string format = "csv",
        CancellationToken cancellationToken = default)
    {
        var query = new ExportAdminIndividualWalletsQuery(search, status, format);
        var result = await _sender.Send(query, cancellationToken);
        return File(result.Content, result.ContentType, result.FileName);
    }
}
