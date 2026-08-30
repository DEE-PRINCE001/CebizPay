using System.Security.Claims;
using Asp.Versioning;
using CebizPay.Application.UseCases.Wallet.Transfer;
using CebizPay.Domain.Finance.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Wallet operations endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/wallet")]
[Authorize]
public sealed class WalletController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="WalletController"/>.
    /// </summary>
    public WalletController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Executes a peer wallet transfer from the authenticated user's wallet to another CebizPay user's wallet.
    ///
    /// The sender's identity is resolved from the JWT bearer token — do NOT supply sender identity fields.
    /// The canonical Idempotency-Key header (or idempotencyKey body field) must be unique per logical transfer.
    /// Repeated requests with the same key and identical payload return the original result without re-executing.
    /// </summary>
    /// <param name="request">Transfer request body.</param>
    /// <param name="idempotencyKeyHeader">Idempotency key from Idempotency-Key header (optional; falls back to body field).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("transfer/peer")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("FinancialTransferPolicy")]
    public async Task<IActionResult> PeerTransfer(
        [FromBody] PeerTransferRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKeyHeader,
        CancellationToken cancellationToken)
    {
        // Idempotency key: header takes precedence over body field
        var idempotencyKey = idempotencyKeyHeader ?? request.IdempotencyKey;

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new { code = "IDEMPOTENCY_KEY_REQUIRED", message = "Idempotency-Key header or idempotencyKey body field is required." });
        }

        var command = new PeerTransferCommand(
            RecipientIdentifier: request.RecipientIdentifier,
            Amount: request.Amount,
            Currency: request.Currency,
            TransactionPin: request.TransactionPin,
            IdempotencyKey: idempotencyKey,
            OrganizationContext: request.OrganizationContext);

        var response = await _sender.Send(command, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Executes an outbound bank transfer from the authenticated user's wallet to an external commercial bank account.
    ///
    /// Funds and applicable fees are debited immediately into the platform bank transfer clearing account in PENDING status.
    /// The canonical Idempotency-Key header (or idempotencyKey body field) must be unique per logical transfer.
    /// Repeated requests with the same key return the initial result without duplicate debits.
    /// </summary>
    /// <param name="request">Bank transfer request body.</param>
    /// <param name="idempotencyKeyHeader">Idempotency key from Idempotency-Key header (optional; falls back to body field).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("transfer/bank")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("FinancialTransferPolicy")]
    public async Task<IActionResult> BankTransfer(
        [FromBody] BankTransferRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKeyHeader,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = idempotencyKeyHeader ?? request.IdempotencyKey;

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new { code = "IDEMPOTENCY_KEY_REQUIRED", message = "Idempotency-Key header or idempotencyKey body field is required." });
        }

        var command = new BankTransferCommand(
            DestinationBankCode: request.DestinationBankCode,
            DestinationAccountNumber: request.DestinationAccountNumber,
            Amount: request.Amount,
            Currency: request.Currency,
            TransactionPin: request.TransactionPin,
            IdempotencyKey: idempotencyKey,
            OrganizationContext: request.OrganizationContext);

        var response = await _sender.Send(command, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Validates and resolves the beneficiary account name for a destination bank account.
    /// </summary>
    [HttpGet("transfer/resolve-account")]
    public async Task<IActionResult> ResolveBankAccount(
        [FromQuery] string bankCode,
        [FromQuery] string accountNumber,
        [FromServices] CebizPay.Application.Common.Interfaces.Finance.IBankAccountResolver accountResolver,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(bankCode) || string.IsNullOrWhiteSpace(accountNumber))
        {
            return BadRequest(new { code = "INVALID_REQUEST", message = "bankCode and accountNumber query parameters are required." });
        }

        var result = await accountResolver.ResolveAsync(bankCode, accountNumber, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(new { code = "ACCOUNT_RESOLUTION_FAILED", message = result.ErrorMessage ?? "Could not resolve bank account name." });
        }

        return Ok(new
        {
            accountNumber = result.AccountNumber,
            accountName = result.AccountName,
            bankCode = result.BankCode
        });
    }

    /// <summary>
    /// Retrieves all external funding accounts attached to the user's or organization's wallet.
    /// </summary>
    [HttpGet("external-accounts")]
    public async Task<IActionResult> GetExternalAccounts(
        [FromQuery] Guid? organizationId,
        [FromQuery] CebizPay.Domain.Finance.Enums.Currency? currency,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var query = new CebizPay.Application.UseCases.Wallet.ExternalAccounts.GetExternalFundingAccountsQuery(
            CurrentUserId: userId,
            OrganizationId: organizationId,
            Currency: currency);

        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets a specific external funding account by ID.
    /// </summary>
    [HttpGet("external-accounts/{id:guid}")]
    public async Task<IActionResult> GetExternalFundingAccountById(
        [FromRoute] Guid id,
        [FromQuery] Guid? organizationId,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var query = new CebizPay.Application.UseCases.Wallet.ExternalAccounts.GetExternalFundingAccountByIdQuery(
            AccountId: id,
            CurrentUserId: userId,
            OrganizationId: organizationId);

        var result = await _sender.Send(query, cancellationToken);
        if (result == null)
            return NotFound(new { message = $"External funding account '{id}' not found." });

        return Ok(result);
    }

    /// <summary>
    /// Provisions a new Monnify reserved virtual account and links it as an external funding account.
    /// </summary>
    [HttpPost("external-accounts/monnify")]
    public async Task<IActionResult> ProvisionMonnifyAccount(
        [FromQuery] Guid? organizationId,
        [FromQuery] Currency currency = Currency.NGN,
        CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var command = new CebizPay.Application.UseCases.Wallet.ExternalAccounts.ProvisionMonnifyExternalFundingAccountCommand(
            CurrentUserId: userId,
            OrganizationId: organizationId,
            Currency: currency);

        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Designates an external funding account as primary for the user's or organization's wallet.
    /// </summary>
    [HttpPost("external-accounts/{id:guid}/primary")]
    public async Task<IActionResult> SetPrimaryAccount(
        [FromRoute] Guid id,
        [FromQuery] Guid? organizationId,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var command = new CebizPay.Application.UseCases.Wallet.ExternalAccounts.SetPrimaryExternalFundingAccountCommand(
            AccountId: id,
            CurrentUserId: userId,
            OrganizationId: organizationId);

        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deactivates / suspends an external funding account.
    /// </summary>
    [HttpDelete("external-accounts/{id:guid}")]
    public async Task<IActionResult> DeactivateAccount(
        [FromRoute] Guid id,
        [FromQuery] Guid? organizationId,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var command = new CebizPay.Application.UseCases.Wallet.ExternalAccounts.DeactivateExternalFundingAccountCommand(
            AccountId: id,
            CurrentUserId: userId,
            OrganizationId: organizationId);

        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets the details and double-entry ledger status of a funding transaction by ID.
    /// </summary>
    [HttpGet("funding/{id:guid}")]
    public async Task<IActionResult> GetFundingTransaction(
        [FromRoute] Guid id,
        [FromQuery] Guid? organizationId,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var query = new CebizPay.Application.UseCases.Wallet.ExternalAccounts.GetFundingTransactionByIdQuery(
            FundingId: id,
            CurrentUserId: userId,
            OrganizationId: organizationId);

        var result = await _sender.Send(query, cancellationToken);
        if (result == null)
            return NotFound(new { message = $"Funding transaction '{id}' not found." });

        return Ok(result);
    }
}

/// <summary>
/// Request body DTO for peer wallet transfer.
/// </summary>
/// <param name="RecipientIdentifier">Email or phone number of the recipient CebizPay user.</param>
/// <param name="Amount">Transfer amount (positive decimal).</param>
/// <param name="Currency">V1 transactional currency: NGN, INTERNATIONAL_NGN, or USDT.</param>
/// <param name="TransactionPin">4-digit numeric transaction PIN.</param>
/// <param name="IdempotencyKey">Client-supplied idempotency key (also accepted as Idempotency-Key header).</param>
/// <param name="OrganizationContext">
/// Optional organization ID. If provided, the transfer is from the organization's wallet.
/// If null, the transfer is from the user's personal wallet.
/// </param>
public sealed record PeerTransferRequest(
    string RecipientIdentifier,
    decimal Amount,
    string Currency,
    string TransactionPin,
    string? IdempotencyKey = null,
    Guid? OrganizationContext = null);

/// <summary>
/// Request body DTO for bank transfer.
/// </summary>
/// <param name="DestinationBankCode">Destination bank institution code (e.g. "058", "044").</param>
/// <param name="DestinationAccountNumber">10-digit NUBAN destination account number.</param>
/// <param name="Amount">Transfer amount (positive decimal).</param>
/// <param name="Currency">V1 transactional currency: NGN, INTERNATIONAL_NGN, or USDT.</param>
/// <param name="TransactionPin">4-digit numeric transaction PIN.</param>
/// <param name="IdempotencyKey">Client-supplied idempotency key (also accepted as Idempotency-Key header).</param>
/// <param name="OrganizationContext">Optional organization ID if transferring from corporate wallet.</param>
public sealed record BankTransferRequest(
    string DestinationBankCode,
    string DestinationAccountNumber,
    decimal Amount,
    string Currency,
    string TransactionPin,
    string? IdempotencyKey = null,
    Guid? OrganizationContext = null);

