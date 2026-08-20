namespace CebizPay.Application.UseCases.Wallet.Transfer;

/// <summary>
/// Response DTO returned following a bank transfer operation.
/// </summary>
/// <param name="TransactionReference">Central ledger business reference (e.g. "CBZBT-XXXXX").</param>
/// <param name="Status">Current status: "PENDING", "PROCESSING", "COMPLETED", or "FAILED".</param>
/// <param name="Amount">Principal amount transferred.</param>
/// <param name="Currency">Transactional currency (NGN, INTERNATIONAL_NGN, USDT).</param>
/// <param name="FeeAmount">Calculated fee debited for this transfer.</param>
/// <param name="TotalDebited">Total funds debited from sender (Amount + FeeAmount).</param>
/// <param name="DestinationBankCode">Destination bank institution code.</param>
/// <param name="DestinationAccountNumber">Masked destination account number (e.g. "******1234").</param>
/// <param name="DestinationAccountName">Destination beneficiary account name, if resolved.</param>
/// <param name="AppliedFeePolicyVersion">Applied fee policy version number, if configured.</param>
/// <param name="CreatedAtUtc">Creation timestamp in UTC.</param>
public sealed record BankTransferResponseDto(
    string TransactionReference,
    string Status,
    decimal Amount,
    string Currency,
    decimal FeeAmount,
    decimal TotalDebited,
    string DestinationBankCode,
    string DestinationAccountNumber,
    string? DestinationAccountName,
    int? AppliedFeePolicyVersion,
    DateTime CreatedAtUtc);
