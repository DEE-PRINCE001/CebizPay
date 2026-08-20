using MediatR;

namespace CebizPay.Application.UseCases.Wallet.Transfer;

/// <summary>
/// Command to definitively mark a bank transfer as FAILED and execute an atomic ledger reversal,
/// refunding principal and fee balances back to the sender wallet.
/// </summary>
public sealed record ReverseBankTransferCommand(
    Guid BankTransferId,
    string Reason,
    string InitiatedByUserId) : IRequest<BankTransferResponseDto>;
