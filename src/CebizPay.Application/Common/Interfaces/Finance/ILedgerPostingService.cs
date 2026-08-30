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
    /// Posts an external funding account deposit (e.g. Monnify Reserved Virtual Account) with full platform fee policy accounting.
    /// Invariant: DEBIT Inbound Clearing (grossAmount), CREDIT Customer Wallet (netCreditedAmount), CREDIT Platform Fee (feeAmount).
    /// Total Debits == Total Credits.
    /// Materializes recipient wallet available balance (+netCreditedAmount) and records FundingTransaction as COMPLETED.
    /// </summary>
    Task<(LedgerTransaction Transaction, FundingTransaction Funding)> PostExternalFundingAccountCreditCoreAsync(
        Guid walletId,
        Guid externalFundingAccountId,
        decimal grossAmount,
        decimal feeAmount,
        decimal netCreditedAmount,
        decimal providerFeeAmount,
        Currency currency,
        PaymentProvider provider,
        string providerTransactionReference,
        string? providerEventReference,
        Guid? feePolicyId,
        int? feePolicyVersion,
        FeeBearer? feeBearer,
        FundingChannel channel = FundingChannel.VirtualAccount,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts a card funding credit (one-time or tokenized charge) with full platform fee policy accounting.
    /// Invariant: DEBIT Inbound Clearing (grossAmount), CREDIT Customer Wallet (netCreditedAmount), CREDIT Platform Fee (feeAmount).
    /// Total Debits == Total Credits.
    /// Materializes recipient wallet available balance (+netCreditedAmount) and records FundingTransaction as COMPLETED.
    /// </summary>
    Task<(LedgerTransaction Transaction, FundingTransaction Funding)> PostCardFundingCreditCoreAsync(
        Guid walletId,
        decimal grossAmount,
        decimal feeAmount,
        decimal netCreditedAmount,
        decimal providerFeeAmount,
        Currency currency,
        PaymentProvider provider,
        string providerTransactionReference,
        string? providerEventReference,
        Guid? feePolicyId,
        int? feePolicyVersion,
        FeeBearer? feeBearer,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts an atomic double-entry reversal for a card refund against the customer wallet.
    /// Performs row-level locking on the customer wallet and verifies balance sufficiency.
    /// If customer balance is insufficient for full immediate reversal, does NOT create a negative wallet balance,
    /// and instead returns a flag or triggers RecoveryOutstanding.
    ///
    /// Ledger entries created:
    ///   DEBIT  Customer Wallet Account    amount
    ///   CREDIT Inbound Clearing Account   amount
    /// </summary>
    Task<(LedgerTransaction Transaction, CebizPay.Domain.Payments.Entities.CardRefund Refund)> PostCardRefundReversalCoreAsync(
        Guid refundId,
        Guid fundingTransactionId,
        decimal amount,
        Currency currency,
        string refundReference,
        string? providerRefundReference,
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

    /// <summary>
    /// Ensures that a platform savings pool clearing account exists for a given currency.
    /// </summary>
    Task<LedgerAccount> GetOrCreateSavingsPoolAccountAsync(Currency currency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures that a dedicated pooled clearing account exists for a given thrift group.
    /// </summary>
    Task<LedgerAccount> GetOrCreateThriftPoolAccountAsync(Guid thriftGroupId, Currency currency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts an atomic savings contribution debiting the customer wallet and crediting the platform savings pool account.
    /// Performs row-level locking on the customer wallet and verifies balance sufficiency.
    /// </summary>
    Task<LedgerTransaction> PostSavingsContributionCoreAsync(
        Guid userWalletId,
        decimal amount,
        Currency currency,
        string reference,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts an atomic savings withdrawal debiting the platform savings pool account and crediting the customer wallet.
    /// Performs row-level locking on the recipient wallet.
    /// </summary>
    Task<LedgerTransaction> PostSavingsWithdrawalCoreAsync(
        Guid userWalletId,
        decimal payoutAmount,
        Currency currency,
        string reference,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts an atomic thrift contribution debiting the member wallet and crediting the thrift group pool account.
    /// Performs row-level locking on the member wallet and verifies balance sufficiency.
    /// </summary>
    Task<LedgerTransaction> PostThriftContributionCoreAsync(
        Guid memberWalletId,
        Guid thriftGroupId,
        decimal amount,
        Currency currency,
        string reference,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts an atomic thrift pool payout debiting the thrift group pool account and crediting the beneficiary member wallet.
    /// Performs row-level locking on the beneficiary wallet.
    /// </summary>
    Task<LedgerTransaction> PostThriftPayoutCoreAsync(
        Guid beneficiaryWalletId,
        Guid thriftGroupId,
        decimal amount,
        Currency currency,
        string reference,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts an atomic thrift reimbursement refunding a departing member's net contributions from the thrift pool account.
    /// Performs row-level locking on the member wallet.
    /// </summary>
    /// <summary>
    /// Posts an atomic thrift reimbursement refunding a departing member's net contributions from the thrift pool account.
    /// Performs row-level locking on the member wallet.
    /// </summary>
    Task<LedgerTransaction> PostThriftReimbursementCoreAsync(
        Guid memberWalletId,
        Guid thriftGroupId,
        decimal amount,
        Currency currency,
        string reference,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures that a platform VAS clearing / provider payable account exists for a given currency.
    /// </summary>
    Task<LedgerAccount> GetOrCreateVasClearingAccountAsync(Currency currency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts an atomic VAS purchase debit from a customer wallet to the platform VAS clearing account.
    /// Performs row-level locking on the customer wallet and verifies balance sufficiency.
    /// Creates the pending <see cref="CebizPay.Domain.Vas.Entities.VasTransaction"/> entity in the same atomic transaction.
    ///
    /// Ledger entries created:
    ///   DEBIT  Customer Wallet Account    amount
    ///   CREDIT VAS Clearing Account       amount
    /// </summary>
    Task<(LedgerTransaction Transaction, CebizPay.Domain.Vas.Entities.VasTransaction VasTransaction)> PostVasPurchaseDebitCoreAsync(
        Guid customerWalletId,
        Guid vasClearingAccountId,
        decimal amount,
        Currency currency,
        string userId,
        Guid? organizationId,
        string phoneNumber,
        CebizPay.Domain.Vas.Enums.VasNetwork network,
        CebizPay.Domain.Vas.Enums.VasType type,
        string? productCode,
        string? productName,
        string reference,
        string? idempotencyKey = null,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts an atomic reversal of a failed VAS purchase transaction, refunding the debited funds
    /// back from the VAS clearing account to the customer wallet.
    /// </summary>
    Task<LedgerTransaction> PostVasPurchaseReversalCoreAsync(
        Guid vasTransactionId,
        string reason,
        CancellationToken cancellationToken = default);
}
