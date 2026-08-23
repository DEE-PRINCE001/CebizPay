using System.Security.Claims;
using CebizPay.Application.Common.Interfaces.Savings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// API controller for user-facing and workplace staff savings operations: plan preview, account opening, contributions, and withdrawals.
/// </summary>
[ApiController]
[Route("api/v1/work/savings")]
[Authorize]
public sealed class StaffSavingsController : ControllerBase
{
    private readonly ISavingsService _savingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="StaffSavingsController"/> class.
    /// </summary>
    public StaffSavingsController(ISavingsService savingsService)
    {
        _savingsService = savingsService;
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("User ID is missing from token.");
    }

    private Guid? GetOrganizationId()
    {
        var orgIdClaim = User.FindFirstValue("OrganizationId") ?? User.FindFirstValue("org_id");
        if (!string.IsNullOrEmpty(orgIdClaim) && Guid.TryParse(orgIdClaim, out var orgId))
        {
            return orgId;
        }
        return null;
    }

    /// <summary>
    /// Previews deterministic interest, maturity payout, and early exit penalties for a prospective savings plan.
    /// </summary>
    [HttpPost("preview")]
    [ProducesResponseType(typeof(SavingsPreviewResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewSavings(
        [FromBody] SavingsPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _savingsService.PreviewSavingsAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Opens a new savings account instance and deposits initial funds from the user's wallet.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SavingsAccountDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> OpenAccount(
        [FromBody] OpenSavingsAccountRequest request,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var account = await _savingsService.OpenAccountAsync(userId, request, idempotencyKey, cancellationToken);
        return CreatedAtAction(nameof(GetAccountById), new { id = account.Id }, account);
    }

    /// <summary>
    /// Lists all savings accounts owned by the authenticated user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SavingsAccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccounts(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var accounts = await _savingsService.GetAccountsAsync(ownerUserId: userId, cancellationToken: cancellationToken);
        return Ok(accounts);
    }

    /// <summary>
    /// Returns the details of a specific savings account.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SavingsAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAccountById(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var orgId = GetOrganizationId();
        var account = await _savingsService.GetAccountByIdAsync(id, userId, orgId, cancellationToken);
        if (account == null)
            return NotFound();

        return Ok(account);
    }

    /// <summary>
    /// Deposits a recurring or ad-hoc financial contribution into an active savings account from the user's wallet.
    /// </summary>
    [HttpPost("{id:guid}/contribute")]
    [ProducesResponseType(typeof(SavingsAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Contribute(
        Guid id,
        [FromBody] SavingsContributeRequest request,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var account = await _savingsService.ContributeAsync(id, userId, request.Amount, idempotencyKey ?? request.IdempotencyKey, cancellationToken);
        return Ok(account);
    }

    /// <summary>
    /// Previews withdrawal payout, accrued interest forfeiture, and principal penalty terms.
    /// </summary>
    [HttpPost("{id:guid}/withdraw/preview")]
    [ProducesResponseType(typeof(SavingsPreviewResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewWithdrawal(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var preview = await _savingsService.PreviewWithdrawalAsync(id, userId, cancellationToken);
        return Ok(preview);
    }

    /// <summary>
    /// Liquidates and withdraws funds from a savings account to the user's wallet via the central double-entry ledger.
    /// </summary>
    [HttpPost("{id:guid}/withdraw")]
    [ProducesResponseType(typeof(SavingsWithdrawalResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Withdraw(
        Guid id,
        [FromBody] SavingsWithdrawRequest? request,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _savingsService.WithdrawAsync(id, userId, idempotencyKey ?? request?.IdempotencyKey, cancellationToken);
        return Ok(result);
    }
}
