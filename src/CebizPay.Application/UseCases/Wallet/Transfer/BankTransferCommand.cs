using MediatR;

namespace CebizPay.Application.UseCases.Wallet.Transfer;

/// <summary>
/// Command to execute an outbound bank transfer.
/// Funds are immediately debited from the sender's wallet into the Bank Transfer Clearing account in PENDING status.
/// </summary>
/// <param name="DestinationBankCode">Destination bank institution code (e.g. "058", "044").</param>
/// <param name="DestinationAccountNumber">10-digit NUBAN destination account number.</param>
/// <param name="Amount">Transfer amount (positive decimal).</param>
/// <param name="Currency">Transactional currency string ("NGN", "INTERNATIONAL_NGN", or "USDT").</param>
/// <param name="TransactionPin">4-digit numeric transaction PIN.</param>
/// <param name="IdempotencyKey">Unique idempotency key.</param>
/// <param name="OrganizationContext">Optional Organization ID if initiating from an organization wallet.</param>
public sealed record BankTransferCommand(
    string DestinationBankCode,
    string DestinationAccountNumber,
    decimal Amount,
    string Currency,
    string TransactionPin,
    string IdempotencyKey,
    Guid? OrganizationContext = null) : IRequest<BankTransferResponseDto>;
