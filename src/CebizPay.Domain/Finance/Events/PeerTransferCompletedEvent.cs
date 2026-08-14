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
