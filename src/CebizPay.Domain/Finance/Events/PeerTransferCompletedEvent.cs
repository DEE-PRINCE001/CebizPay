namespace CebizPay.Domain.Finance.Events;

/// <summary>
/// Domain event published via Outbox after a successful peer wallet transfer.
/// Contains only safe identifiers and financial metadata — no PIN, no auth tokens, no sensitive KYC data.
/// </summary>
public sealed record PeerTransferCompletedEvent(
    Guid TransactionId,
    string TransactionReference,
    Guid SenderWalletId,
    Guid RecipientWalletId,
    decimal Amount,
    string Currency,
    decimal FeeAmount,
    string FeeCurrency,
    int? FeePolicyVersion,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published via Outbox when an outbound bank transfer is created (immediate debit committed).
/// Contains only safe identifiers and financial metadata — account number is masked, no PINs/secrets.
/// </summary>
public sealed record BankTransferCreatedEvent(
    Guid TransferId,
    string TransactionReference,
    Guid SenderWalletId,
    string DestinationBankCode,
    string MaskedDestinationAccountNumber,
    decimal Amount,
    string Currency,
    decimal FeeAmount,
    string FeeCurrency,
    int? FeePolicyVersion,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when an outbound bank transfer reaches COMPLETED state.
/// </summary>
public sealed record BankTransferCompletedEvent(
    Guid TransferId,
    string TransactionReference,
    string? ProviderReference,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when an outbound bank transfer reaches FAILED state.
/// </summary>
public sealed record BankTransferFailedEvent(
    Guid TransferId,
    string TransactionReference,
    string Reason,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when an outbound bank transfer is reversed and funds are restored to sender.
/// </summary>
public sealed record BankTransferReversedEvent(
    Guid TransferId,
    string OriginalTransactionReference,
    Guid ReversalTransactionId,
    string ReversalTransactionReference,
    decimal Amount,
    string Currency,
    decimal FeeAmount,
    string Reason,
    DateTime OccurredOnUtc);

