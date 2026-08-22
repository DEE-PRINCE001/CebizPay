using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;

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

    /// <summary>
    /// Returns the platform FeeRevenue ledger account for the given currency, creating it if it does not yet exist.
    /// This is the account that receives platform fee revenue for peer transfers.
    /// </summary>
    Task<LedgerAccount> GetOrCreatePlatformFeeAccountAsync(Currency currency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts a 3-entry peer-transfer ledger transaction within an already-begun ambient database transaction.
    /// Does NOT start or commit its own transaction — the caller is responsible for the outer transaction boundary.
    /// Performs deterministic wallet locking (lower GUID first) to prevent deadlocks.
    /// Re-validates sender balance after acquiring locks (TOCTOU protection).
    ///
    /// Ledger entries created:
    ///   DEBIT  Sender LedgerAccount      transferAmount + feeAmount
    ///   CREDIT Recipient LedgerAccount   transferAmount
    ///   CREDIT Platform Fee Account      feeAmount
    ///
    /// When feeAmount == 0, only 2 entries are created (no zero-value fee entry).
    /// </summary>
    Task<LedgerTransaction> PostPeerTransferCoreAsync(
        Guid senderWalletId,
        Guid recipientWalletId,
        Guid platformFeeAccountId,
        decimal transferAmount,
        decimal feeAmount,
        Currency currency,
        string reference,
        string? idempotencyKey,
        string? description,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the platform PlatformClearing ledger account for bank transfers for the given currency, creating it if it does not yet exist.
    /// This is the internal clearing account that temporarily holds debited bank transfer funds prior to external settlement.
    /// </summary>
    Task<LedgerAccount> GetOrCreateBankTransferClearingAccountAsync(Currency currency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts a bank transfer debit within an already-begun ambient database transaction.
    /// Does NOT start or commit its own transaction — the caller is responsible for the outer transaction boundary.
    /// Performs row-level locking on the sender wallet.
    /// Re-validates sender balance and status after acquiring lock (TOCTOU protection).
    ///
    /// Ledger entries created:
    ///   DEBIT  Sender LedgerAccount               transferAmount + feeAmount
    ///   CREDIT Bank Transfer Clearing Account     transferAmount
    ///   CREDIT Platform Fee Account               feeAmount (if feeAmount > 0)
    ///
    /// Creates a LedgerTransaction (Type: BankTransfer, Status: Pending) and BankTransfer (Status: Pending).
    /// </summary>
    Task<(LedgerTransaction Transaction, BankTransfer Transfer)> PostBankTransferDebitCoreAsync(
        Guid senderWalletId,
        Guid clearingAccountId,
        Guid platformFeeAccountId,
        decimal transferAmount,
        decimal feeAmount,
        Currency currency,
        string destinationBankCode,
        string destinationAccountNumber,
        string? destinationAccountName,
        Guid? feePolicyId,
        int? feePolicyVersion,
        string reference,
        string? idempotencyKey,
        string? description,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts an atomic reversal of a pending/processing bank transfer upon definitive external failure.
    /// Creates a new reversal LedgerTransaction with offsetting double-entry ledger entries and restores sender balance.
    /// Marks the BankTransfer aggregate as FAILED.
    /// </summary>
    Task<LedgerTransaction> PostBankTransferReversalCoreAsync(
        Guid bankTransferId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the platform Inbound Clearing ledger account for deposits (DVA / Card) for the given currency.
    /// </summary>
    Task<LedgerAccount> GetOrCreateInboundClearingAccountAsync(Currency currency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts an inbound funding credit (virtual account deposit or card funding) within an ambient or new transaction.
    /// Performs row-level locking on the recipient wallet.
    ///
    /// Ledger entries created:
    ///   DEBIT  Inbound Clearing Account   amount
    ///   CREDIT Customer Wallet Account    amount
    ///
    /// Materializes recipient wallet available balance (+amount) and updates FundingTransaction to COMPLETED.
    /// </summary>
    Task<(LedgerTransaction Transaction, FundingTransaction Funding)> PostInboundFundingCreditCoreAsync(
        Guid walletId,
        Guid? virtualAccountId,
        decimal amount,
        Currency currency,
        PaymentProvider provider,
        string providerTransactionReference,
        FundingChannel channel,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts an atomic corporate payroll disbursement from an organization wallet to an employee wallet.
    /// Performs ordered row-level locking on both wallets to prevent deadlocks and double-spends.
    /// Re-validates organization balance sufficiency after acquiring lock.
    ///
    /// Ledger entries created:
    ///   DEBIT  Organization Wallet Account    amount
    ///   CREDIT Employee Wallet Account        amount
    ///
    /// Updates organization balance (-amount) and employee balance (+amount).
    /// Creates a LedgerTransaction (Type: Payroll, Status: Completed).
    /// </summary>
    Task<LedgerTransaction> PostPayrollDisbursementCoreAsync(
        Guid organizationWalletId,
        Guid employeeWalletId,
        decimal amount,
        Currency currency,
        string reference,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures that a platform loan fund / disbursement clearing account exists for a given currency.
    /// </summary>
    Task<LedgerAccount> GetOrCreateLoanFundAccountAsync(Currency currency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures that a platform loan receivable account exists for a given currency.
    /// </summary>
    Task<LedgerAccount> GetOrCreateLoanReceivableAccountAsync(Currency currency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts an atomic corporate loan principal disbursement from the system loan funding account to an employee wallet.
    /// Performs row-level locking on the recipient wallet.
    ///
    /// Ledger entries created:
    ///   DEBIT  Loan Fund / Disbursement Account    amount
    ///   CREDIT Employee Wallet Account             amount
    ///
    /// Materializes recipient employee wallet balance (+amount).
    /// Creates a LedgerTransaction (Type: LoanDisbursement, Status: Completed).
    /// </summary>
    Task<LedgerTransaction> PostLoanDisbursementCoreAsync(
        Guid employeeWalletId,
        decimal amount,
        Currency currency,
        string reference,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts an atomic loan direct repayment from an employee wallet to the loan receivable account.
    /// Performs row-level locking on the employee wallet and verifies balance sufficiency.
    ///
    /// Ledger entries created:
    ///   DEBIT  Employee Wallet Account    amount
    ///   CREDIT Loan Receivable Account    amount
    ///
    /// Deducts amount from employee wallet balance (-amount).
    /// Creates a LedgerTransaction (Type: LoanRepayment, Status: Completed).
    /// </summary>
    Task<LedgerTransaction> PostLoanRepaymentCoreAsync(
        Guid employeeWalletId,
        decimal amount,
        Currency currency,
        string reference,
        string? description = null,
        CancellationToken cancellationToken = default);
}
