#pragma warning disable CS1591
using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Organizations.Erp;
using CebizPay.Domain.Erp.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// API controller for ERP Operating Expenses.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/org/expenses")]
[Authorize]
public sealed class OrgExpensesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentOrganizationContext _orgContext;

    public OrgExpensesController(ISender sender, ICurrentOrganizationContext orgContext)
    {
        _sender = sender;
        _orgContext = orgContext;
    }

    private Guid GetOrganizationId()
    {
        var orgId = _orgContext.CurrentOrganizationId;
        if (!orgId.HasValue || orgId.Value == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Active organization context is required.");
        }
        return orgId.Value;
    }

    /// <summary>Creates a new operating expense.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateExpense([FromBody] CreateOperatingExpenseApiRequest request, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var command = new CreateOperatingExpenseCommand(
            orgId,
            request.Category,
            request.Description,
            request.Amount,
            request.ExpenseDate,
            request.PaymentMethod,
            request.SupplierId,
            request.Currency,
            request.Reference);

        var id = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetExpenseById), new { id }, id);
    }

    /// <summary>Retrieves paged operating expenses for the active organization.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OperatingExpenseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpenses(
        [FromQuery] ExpenseCategory? category,
        [FromQuery] ExpenseStatus? status,
        [FromQuery] Guid? supplierId,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var query = new GetOperatingExpensesQuery(orgId, category, status, supplierId, search, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Retrieves operating expense details by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OperatingExpenseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpenseById(Guid id, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var query = new GetOperatingExpenseByIdQuery(orgId, id);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Approves an operating expense.</summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveExpense(Guid id, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        await _sender.Send(new ApproveOperatingExpenseCommand(orgId, id), cancellationToken);
        return Ok();
    }

    /// <summary>Pays an operating expense (via wallet with PIN/Idempotency or manual settlement).</summary>
    [HttpPost("{id:guid}/pay")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> PayExpense(
        Guid id,
        [FromBody] PayOperatingExpenseApiRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var command = new PayOperatingExpenseCommand(
            orgId,
            id,
            request.PaymentMethod,
            request.Pin,
            request.IdempotencyKey,
            request.Reference);

        await _sender.Send(command, cancellationToken);
        return Ok();
    }

    /// <summary>Cancels an operating expense.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelExpense(Guid id, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        await _sender.Send(new CancelOperatingExpenseCommand(orgId, id), cancellationToken);
        return Ok();
    }
}
