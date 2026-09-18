using System.Security.Claims;
using Asp.Versioning;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Admin.Individuals;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Platform Administrative Individuals Directory and Management Controller.
/// Enables platform administrators (SuperAdmin, Admin, Auditor) to inspect individual profiles, KYC documents, transactions, wallets, and savings contracts.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/individuals")]
[Authorize(Roles = "SuperAdmin,Admin,Auditor")]
public sealed class AdminIndividualsController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="AdminIndividualsController"/>.
    /// </summary>
    public AdminIndividualsController(ISender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    /// <summary>
    /// Retrieves a paginated directory of all onboarded individual users across the platform.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AdminIndividualSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetIndividualsDirectory(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? professionalStatus = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAdminIndividualsDirectoryQuery(pageNumber, pageSize, search, status, professionalStatus);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Exports filtered individuals dataset directly as a downloadable CSV file.
    /// </summary>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportIndividuals(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string format = "csv",
        CancellationToken cancellationToken = default)
    {
        var query = new ExportAdminIndividualsQuery(search, status, format);
        var result = await _sender.Send(query, cancellationToken);
        return File(result.Content, result.ContentType, result.FileName);
    }

    /// <summary>
    /// Retrieves full profile, employment, and submitted KYC credential details for an individual user.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AdminIndividualDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetIndividualById(
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAdminIndividualDetailsQuery(id);
        var result = await _sender.Send(query, cancellationToken);

        if (result == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Individual Not Found",
                Detail = $"Individual with identifier '{id}' was not found."
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Retrieves the paginated ledger transaction history for a specific individual.
    /// </summary>
    [HttpGet("{id}/transactions")]
    [ProducesResponseType(typeof(PagedResult<AdminIndividualTransactionItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetIndividualTransactions(
        [FromRoute] string id,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? type = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new GetAdminIndividualTransactionsQuery(id, pageNumber, pageSize, search, type, status);
            var result = await _sender.Send(query, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Individual Not Found",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Retrieves wallet account balances and ledger metadata for a specific individual.
    /// </summary>
    [HttpGet("{id}/wallets")]
    [ProducesResponseType(typeof(AdminIndividualWalletDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetIndividualWallets(
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new GetAdminIndividualWalletQuery(id);
            var result = await _sender.Send(query, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Individual Not Found",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Retrieves active and completed savings plans associated with a specific individual.
    /// </summary>
    [HttpGet("{id}/savings")]
    [ProducesResponseType(typeof(AdminIndividualSavingsListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetIndividualSavings(
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new GetAdminIndividualSavingsQuery(id);
            var result = await _sender.Send(query, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Individual Not Found",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Administratively suspends an individual user profile, blocking outbound debit transfers.
    /// </summary>
    [HttpPatch("{id}/suspend")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(typeof(AdminIndividualStatusResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SuspendIndividual(
        [FromRoute] string id,
        [FromBody] SuspendIndividualRequest request,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("sub")?.Value 
            ?? string.Empty;

        try
        {
            var command = new SuspendIndividualCommand(id, request.Reason, adminUserId);
            var result = await _sender.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Individual Not Found",
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Operation",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Administratively reactivates a suspended individual user profile, restoring outbound debit transfers.
    /// </summary>
    [HttpPatch("{id}/reactivate")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(typeof(AdminIndividualStatusResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ReactivateIndividual(
        [FromRoute] string id,
        [FromBody] ReactivateIndividualRequest request,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("sub")?.Value 
            ?? string.Empty;

        try
        {
            var command = new ReactivateIndividualCommand(id, request.Reason, adminUserId);
            var result = await _sender.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Individual Not Found",
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Operation",
                Detail = ex.Message
            });
        }
    }
}
