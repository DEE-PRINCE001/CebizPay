using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Payroll;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Organization payroll calculation, batch execution, progress monitoring, retries, and payment voucher management endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/org/payroll")]
[Authorize]
public sealed class PayrollController : ControllerBase
{
    private readonly IPayrollCalculationService _calculationService;
    private readonly IPayrollBatchService _batchService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="PayrollController"/>.
    /// </summary>
    public PayrollController(
        IPayrollCalculationService calculationService,
        IPayrollBatchService batchService,
        ICurrentUserService currentUserService,
        ICurrentOrganizationContext orgContext)
    {
        _calculationService = calculationService ?? throw new ArgumentNullException(nameof(calculationService));
        _batchService = batchService ?? throw new ArgumentNullException(nameof(batchService));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
    }

    /// <summary>
    /// Computes and returns a deterministic payroll calculation dry-run without mutating wallets or ledger balances.
    /// </summary>
    [HttpPost("calculate")]
    public async Task<IActionResult> Calculate(
        [FromBody] CalculatePayrollApiRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = _orgContext.CurrentOrganizationId;
        if (!orgId.HasValue || orgId.Value == Guid.Empty)
        {
            return BadRequest(new { code = "ORGANIZATION_CONTEXT_REQUIRED", message = "Active organization context header or claim is required." });
        }

        var result = await _calculationService.CalculatePayrollAsync(
            orgId.Value,
            request.Currency,
            request.Criteria ?? new PayrollSelectionCriteria(),
            cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    /// Creates and enqueues a corporate payroll batch run for asynchronous worker execution.
    /// </summary>
    [HttpPost("execute")]
    public async Task<IActionResult> Execute(
        [FromBody] ExecutePayrollApiRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var orgId = _orgContext.CurrentOrganizationId;
        var userId = _currentUserService.UserId;

        if (!orgId.HasValue || orgId.Value == Guid.Empty)
        {
            return BadRequest(new { code = "ORGANIZATION_CONTEXT_REQUIRED", message = "Active organization context header or claim is required." });
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "User context missing." });
        }

        var batchDto = await _batchService.CreateAndEnqueueBatchAsync(
            organizationId: orgId.Value,
            initiatorUserId: userId,
            currency: request.Currency,
            periodStart: request.PeriodStart,
            periodEnd: request.PeriodEnd,
            criteria: request.Criteria ?? new PayrollSelectionCriteria(),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return AcceptedAtAction(nameof(GetProgress), new { version = "1.0", batchId = batchDto.BatchId }, batchDto);
    }

    /// <summary>
    /// Retrieves aggregate progress statistics and paged line-item details for a payroll batch run.
    /// </summary>
    [HttpGet("{batchId:guid}")]
    public async Task<IActionResult> GetProgress(
        [FromRoute] Guid batchId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var orgId = _orgContext.CurrentOrganizationId;
        if (!orgId.HasValue || orgId.Value == Guid.Empty)
        {
            return BadRequest(new { code = "ORGANIZATION_CONTEXT_REQUIRED", message = "Active organization context is required." });
        }

        var progress = await _batchService.GetBatchProgressAsync(orgId.Value, batchId, pageNumber, pageSize, cancellationToken).ConfigureAwait(false);
        if (progress == null)
        {
            return NotFound(new { code = "PAYROLL_BATCH_NOT_FOUND", message = $"Payroll batch '{batchId}' was not found for this organization." });
        }

        return Ok(progress);
    }

    /// <summary>
    /// Re-queues all eligible failed items in a payroll batch for background worker retry.
    /// </summary>
    [HttpPost("{batchId:guid}/retry-failed")]
    public async Task<IActionResult> RetryFailed(
        [FromRoute] Guid batchId,
        CancellationToken cancellationToken)
    {
        var orgId = _orgContext.CurrentOrganizationId;
        var userId = _currentUserService.UserId;

        if (!orgId.HasValue || orgId.Value == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { code = "INVALID_CONTEXT", message = "Active organization and user context are required." });
        }

        var hasPermission = await _orgContext.HasPermissionAsync(orgId.Value, Domain.Permissions.Permissions.PayrollExecute, cancellationToken).ConfigureAwait(false);
        if (!hasPermission)
        {
            throw new UnauthorizedAccessException("You do not have permission to retry failed payroll items for this organization.");
        }

        var retriedCount = await _batchService.RetryFailedItemsAsync(orgId.Value, batchId, userId, cancellationToken).ConfigureAwait(false);
        return Ok(new { batchId, retriedCount, message = $"{retriedCount} failed item(s) queued for retry." });
    }

    /// <summary>
    /// Cancels a Pending payroll batch run before any line items have commenced financial processing.
    /// </summary>
    [HttpPost("{batchId:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        [FromRoute] Guid batchId,
        CancellationToken cancellationToken)
    {
        var orgId = _orgContext.CurrentOrganizationId;
        var userId = _currentUserService.UserId;

        if (!orgId.HasValue || orgId.Value == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { code = "INVALID_CONTEXT", message = "Active organization and user context are required." });
        }

        var hasPermission = await _orgContext.HasPermissionAsync(orgId.Value, Domain.Permissions.Permissions.PayrollExecute, cancellationToken).ConfigureAwait(false);
        if (!hasPermission)
        {
            throw new UnauthorizedAccessException("You do not have permission to cancel payroll for this organization.");
        }

        await _batchService.CancelBatchAsync(orgId.Value, batchId, userId, cancellationToken).ConfigureAwait(false);
        return Ok(new { batchId, status = "Cancelled", message = "Payroll batch cancelled successfully." });
    }

    /// <summary>
    /// Retrieves an issued Payment Voucher by identifier with tenant isolation.
    /// </summary>
    [HttpGet("vouchers/{id:guid}")]
    public async Task<IActionResult> GetVoucher(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var orgId = _orgContext.CurrentOrganizationId;
        if (!orgId.HasValue || orgId.Value == Guid.Empty)
        {
            return BadRequest(new { code = "ORGANIZATION_CONTEXT_REQUIRED", message = "Active organization context is required." });
        }

        var voucher = await _batchService.GetPaymentVoucherByIdAsync(orgId.Value, id, cancellationToken).ConfigureAwait(false);
        if (voucher == null)
        {
            return NotFound(new { code = "VOUCHER_NOT_FOUND", message = $"Payment voucher '{id}' not found." });
        }

        return Ok(voucher);
    }

    /// <summary>
    /// Updates safe non-financial metadata (BankName, Remarks, Description) on an issued payment voucher.
    /// </summary>
    [HttpPut("vouchers/{id:guid}")]
    public async Task<IActionResult> UpdateVoucherMetadata(
        [FromRoute] Guid id,
        [FromBody] UpdatePaymentVoucherMetadataRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = _orgContext.CurrentOrganizationId;
        var userId = _currentUserService.UserId;

        if (!orgId.HasValue || orgId.Value == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { code = "INVALID_CONTEXT", message = "Active organization and user context are required." });
        }

        var updated = await _batchService.UpdatePaymentVoucherMetadataAsync(orgId.Value, id, userId, request, cancellationToken).ConfigureAwait(false);
        return Ok(updated);
    }
}

/// <summary>
/// API request payload for previewing a dry-run payroll calculation.
/// </summary>
public sealed record CalculatePayrollApiRequest(
    Currency Currency = Currency.NGN,
    PayrollSelectionCriteria? Criteria = null);

/// <summary>
/// API request payload for scheduling a live payroll execution run.
/// </summary>
public sealed record ExecutePayrollApiRequest(
    Currency Currency,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    PayrollSelectionCriteria? Criteria = null);
