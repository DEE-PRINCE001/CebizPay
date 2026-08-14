using MediatR;

namespace CebizPay.Application.UseCases.Wallet.Transfer;

/// <summary>
/// Command to execute a peer wallet transfer from the authenticated user's wallet to another CebizPay user's wallet.
/// The source wallet is resolved from authenticated context — client must NOT send sender identity fields.
/// </summary>
/// <param name="RecipientIdentifier">Email address or phone number of the recipient user.</param>
/// <param name="Amount">Transfer amount. Must be positive. Must use the wallet currency.</param>
/// <param name="Currency">Transfer currency code (NGN, INTERNATIONAL_NGN, USDT). Must be a V1 transactional currency.</param>
/// <param name="TransactionPin">4-digit transaction PIN of the authenticated sender.</param>
/// <param name="IdempotencyKey">Client-supplied idempotency key. Required to prevent duplicate submissions.</param>
/// <param name="OrganizationContext">
/// Optional organization ID. If provided, the transfer is executed from the organization's wallet
/// (the authenticated user must be an authorized active member of that organization).
/// If null, the transfer is executed from the user's personal individual wallet.
/// </param>
public sealed record PeerTransferCommand(
    string RecipientIdentifier,
    decimal Amount,
    string Currency,
    string TransactionPin,
    string IdempotencyKey,
    Guid? OrganizationContext = null) : IRequest<PeerTransferResponseDto>;

/// <summary>
/// Response DTO for a completed or idempotently replayed peer transfer.
/// </summary>
/// <param name="TransactionReference">Stable unique CebizPay financial reference for this transfer.</param>
/// <param name="Status">Transaction status (e.g. "COMPLETED").</param>
/// <param name="Amount">Transfer amount sent to the recipient.</param>
/// <param name="Currency">Transfer currency.</param>
/// <param name="FeeAmount">Fee charged for this transfer.</param>
/// <param name="TotalDebited">Total amount debited from the sender's wallet (Amount + FeeAmount).</param>
/// <param name="RecipientDisplay">Display name or identifier of the recipient (safe to return).</param>
/// <param name="AppliedFeePolicyVersion">The fee policy version used for this transfer.</param>
/// <param name="CreatedAtUtc">UTC timestamp when the transfer was created.</param>
public sealed record PeerTransferResponseDto(
    string TransactionReference,
    string Status,
    decimal Amount,
    string Currency,
    decimal FeeAmount,
    decimal TotalDebited,
    string RecipientDisplay,
    int? AppliedFeePolicyVersion,
    DateTime CreatedAtUtc);
