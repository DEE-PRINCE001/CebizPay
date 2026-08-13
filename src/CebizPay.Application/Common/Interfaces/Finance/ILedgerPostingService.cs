using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Application.Common.Interfaces.Finance;

/// <summary>
/// Contract for central ledger posting operations enforcing double-entry invariants,
/// wallet balance materialization, and PostgreSQL row concurrency locking.
/// </summary>
public interface ILedgerPostingService
{
    /// <summary>
    /// Ensures that an explicit platform FX settlement account exists for a given currency.
    /// </summary>
    Task<LedgerAccount> GetOrCreateSystemSettlementAccountAsync(Currency currency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts a single-currency double-entry transaction between two ledger accounts.
    /// Invariant: Total Debits == Total Credits.
    /// </summary>
    Task<LedgerTransaction> PostSingleCurrencyTransactionAsync(
        Guid sourceLedgerAccountId,
        Guid targetLedgerAccountId,
        decimal amount,
        Currency currency,
        LedgerTransactionType transactionType,
        string? reference = null,
        string? idempotencyKey = null,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts a cross-currency transaction using explicit currency settlement accounts (Option A).
    /// Invariant: Source currency side balances independently; Target currency side balances independently.
    /// FX conversion record is linked and persisted.
    /// </summary>
    Task<(LedgerTransaction Transaction, FxConversion FxRecord)> PostCrossCurrencyTransactionAsync(
        Guid sourceWalletId,
        Guid targetWalletId,
        decimal sourceAmount,
        decimal targetAmount,
        decimal rate,
        string rateProvider,
        DateTime rateTimestamp,
        string? reference = null,
        string? idempotencyKey = null,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs an atomic reversal of a completed transaction by posting offsetting entries.
    /// Invariant: Original entries are untouched; double-entry offsetting entries are recorded.
    /// </summary>
    Task<LedgerTransaction> ReverseTransactionAsync(
        Guid originalTransactionId,
        string reason,
        CancellationToken cancellationToken = default);
}
