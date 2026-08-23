using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Infrastructure.Finance;

/// <summary>
/// Core ledger posting engine implementing atomic double-entry bookkeeping,
/// PostgreSQL row-level locking concurrency protection, and Option A FX settlement.
/// </summary>
public sealed class LedgerPostingService : ILedgerPostingService
{
    private readonly ApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="LedgerPostingService"/>.
    /// </summary>
    public LedgerPostingService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<LedgerAccount> GetOrCreateSystemSettlementAccountAsync(Currency currency, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.AccountType == LedgerAccountType.SystemSettlement && l.Currency == currency, cancellationToken)
            ?? _dbContext.LedgerAccounts.Local
                .FirstOrDefault(l => l.AccountType == LedgerAccountType.SystemSettlement && l.Currency == currency);

        if (existing != null)
        {
            return existing;
        }

        var accountName = $"{currency} FX SETTLEMENT";
        var account = LedgerAccount.CreateSystemAccount(accountName, currency, LedgerAccountType.SystemSettlement);
        _dbContext.LedgerAccounts.Add(account);

        if (_dbContext.Database.CurrentTransaction == null)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                var concurrentAccount = await _dbContext.LedgerAccounts
                    .FirstOrDefaultAsync(l => l.AccountType == LedgerAccountType.SystemSettlement && l.Currency == currency, cancellationToken);
                if (concurrentAccount != null)
                {
                    return concurrentAccount;
                }
                throw;
            }
        }

        return account;
    }

    /// <inheritdoc/>
    public async Task<LedgerTransaction> PostSingleCurrencyTransactionAsync(
        Guid sourceLedgerAccountId,
        Guid targetLedgerAccountId,
        decimal amount,
        Currency currency,
        LedgerTransactionType transactionType,
        string? reference = null,
        string? idempotencyKey = null,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            throw new ArgumentException("Transaction amount must be positive.", nameof(amount));
        if (sourceLedgerAccountId == targetLedgerAccountId)
            throw new ArgumentException("Source and target ledger accounts must be different.", nameof(targetLedgerAccountId));

        using var dbTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Fetch ledger accounts
            var sourceAccount = await _dbContext.LedgerAccounts.FirstOrDefaultAsync(l => l.Id == sourceLedgerAccountId, cancellationToken)
                ?? throw new InvalidOperationException($"Source ledger account '{sourceLedgerAccountId}' not found.");
            var targetAccount = await _dbContext.LedgerAccounts.FirstOrDefaultAsync(l => l.Id == targetLedgerAccountId, cancellationToken)
                ?? throw new InvalidOperationException($"Target ledger account '{targetLedgerAccountId}' not found.");

            if (sourceAccount.Currency != currency || targetAccount.Currency != currency)
            {
                throw new InvalidOperationException($"Currency mismatch. Transaction currency is {currency}, source is {sourceAccount.Currency}, target is {targetAccount.Currency}.");
            }

            Wallet? sourceWallet = null;
            Wallet? targetWallet = null;

            // Deterministic row locking: lock wallets in lower-GUID order first to eliminate deadlocks
            if (sourceAccount.WalletId.HasValue && targetAccount.WalletId.HasValue)
            {
                var idA = sourceAccount.WalletId.Value;
                var idB = targetAccount.WalletId.Value;

                var firstId = idA.CompareTo(idB) < 0 ? idA : idB;
                var secondId = idA.CompareTo(idB) < 0 ? idB : idA;

                var firstWallet = _dbContext.Database.IsNpgsql()
                    ? await _dbContext.Wallets.FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", firstId).FirstOrDefaultAsync(cancellationToken)
                    : await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == firstId, cancellationToken);
                if (firstWallet == null)
                    throw new InvalidOperationException($"Wallet '{firstId}' not found.");

                var secondWallet = _dbContext.Database.IsNpgsql()
                    ? await _dbContext.Wallets.FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", secondId).FirstOrDefaultAsync(cancellationToken)
                    : await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == secondId, cancellationToken);
                if (secondWallet == null)
                    throw new InvalidOperationException($"Wallet '{secondId}' not found.");

                sourceWallet = idA == firstId ? firstWallet : secondWallet;
                targetWallet = idB == firstId ? firstWallet : secondWallet;
            }
            else if (sourceAccount.WalletId.HasValue)
            {
                var id = sourceAccount.WalletId.Value;
                sourceWallet = _dbContext.Database.IsNpgsql()
                    ? await _dbContext.Wallets.FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", id).FirstOrDefaultAsync(cancellationToken)
                    : await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
                if (sourceWallet == null)
                    throw new InvalidOperationException($"Source wallet '{id}' not found.");
            }
            else if (targetAccount.WalletId.HasValue)
            {
                var id = targetAccount.WalletId.Value;
                targetWallet = _dbContext.Database.IsNpgsql()
                    ? await _dbContext.Wallets.FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", id).FirstOrDefaultAsync(cancellationToken)
                    : await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
                if (targetWallet == null)
                    throw new InvalidOperationException($"Target wallet '{id}' not found.");
            }

            if (sourceWallet != null)
            {
                if (sourceWallet.Currency != currency)
                    throw new InvalidOperationException("Source wallet currency does not match transaction currency.");

                sourceWallet.Debit(amount);
            }

            if (targetWallet != null)
            {
                if (targetWallet.Currency != currency)
                    throw new InvalidOperationException("Target wallet currency does not match transaction currency.");

                targetWallet.Credit(amount);
            }

            // Double-entry validation invariant: Total Debits == Total Credits
            var transaction = new LedgerTransaction(transactionType, reference, idempotencyKey, description);
            transaction.Complete(DateTime.UtcNow);

            var debitEntry = new LedgerEntry(transaction.Id, sourceAccount.Id, LedgerEntryDirection.Debit, amount, currency, sequence: 1);
            var creditEntry = new LedgerEntry(transaction.Id, targetAccount.Id, LedgerEntryDirection.Credit, amount, currency, sequence: 2);

            _dbContext.LedgerTransactions.Add(transaction);
            _dbContext.LedgerEntries.AddRange(debitEntry, creditEntry);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);

            return transaction;
        }
        catch
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<(LedgerTransaction Transaction, FxConversion FxRecord)> PostCrossCurrencyTransactionAsync(
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
        CancellationToken cancellationToken = default)
    {
        if (sourceAmount <= 0)
            throw new ArgumentException("Source amount must be positive.", nameof(sourceAmount));
        if (targetAmount <= 0)
            throw new ArgumentException("Target amount must be positive.", nameof(targetAmount));
        if (rate <= 0)
            throw new ArgumentException("Rate must be positive.", nameof(rate));

        using var dbTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Deterministic row locking for cross-currency wallets
            var firstId = sourceWalletId.CompareTo(targetWalletId) < 0 ? sourceWalletId : targetWalletId;
            var secondId = sourceWalletId.CompareTo(targetWalletId) < 0 ? targetWalletId : sourceWalletId;

            var firstWallet = _dbContext.Database.IsNpgsql()
                ? await _dbContext.Wallets.FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", firstId).FirstOrDefaultAsync(cancellationToken)
                : await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == firstId, cancellationToken);
            if (firstWallet == null)
                throw new InvalidOperationException($"Wallet '{firstId}' not found.");

            var secondWallet = _dbContext.Database.IsNpgsql()
                ? await _dbContext.Wallets.FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", secondId).FirstOrDefaultAsync(cancellationToken)
                : await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == secondId, cancellationToken);
            if (secondWallet == null)
                throw new InvalidOperationException($"Wallet '{secondId}' not found.");

            var sourceWallet = sourceWalletId == firstId ? firstWallet : secondWallet;
            var targetWallet = targetWalletId == firstId ? firstWallet : secondWallet;

            if (sourceWallet.Currency == targetWallet.Currency)
            {
                throw new InvalidOperationException("Cross-currency operations require different source and target wallet currencies.");
            }

            var sourceAccount = await _dbContext.LedgerAccounts.FirstOrDefaultAsync(l => l.WalletId == sourceWallet.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Ledger account for source wallet '{sourceWallet.Id}' not found.");
            var targetAccount = await _dbContext.LedgerAccounts.FirstOrDefaultAsync(l => l.WalletId == targetWallet.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Ledger account for target wallet '{targetWallet.Id}' not found.");

            // Get FX settlement clearing accounts for each currency (Option A)
            var sourceFxSettlement = await GetOrCreateSystemSettlementAccountAsync(sourceWallet.Currency, cancellationToken);
            var targetFxSettlement = await GetOrCreateSystemSettlementAccountAsync(targetWallet.Currency, cancellationToken);

            // Materialize wallet balances
            sourceWallet.Debit(sourceAmount);
            targetWallet.Credit(targetAmount);

            // Create atomic ledger transaction & double-entry entries
            var transaction = new LedgerTransaction(LedgerTransactionType.FxConversion, reference, idempotencyKey, description);
            transaction.Complete(DateTime.UtcNow);

            // Source currency side double-entry (Total Debits == Total Credits in Source Currency)
            var entry1 = new LedgerEntry(transaction.Id, sourceAccount.Id, LedgerEntryDirection.Debit, sourceAmount, sourceWallet.Currency, sequence: 1);
            var entry2 = new LedgerEntry(transaction.Id, sourceFxSettlement.Id, LedgerEntryDirection.Credit, sourceAmount, sourceWallet.Currency, sequence: 2);

            // Target currency side double-entry (Total Debits == Total Credits in Target Currency)
            var entry3 = new LedgerEntry(transaction.Id, targetFxSettlement.Id, LedgerEntryDirection.Debit, targetAmount, targetWallet.Currency, sequence: 3);
            var entry4 = new LedgerEntry(transaction.Id, targetAccount.Id, LedgerEntryDirection.Credit, targetAmount, targetWallet.Currency, sequence: 4);

            var fxConversion = new FxConversion(
                transaction.Id,
                sourceWallet.Currency,
                targetWallet.Currency,
                sourceAmount,
                targetAmount,
                rate,
                rateProvider,
                rateTimestamp);

            _dbContext.LedgerTransactions.Add(transaction);
            _dbContext.LedgerEntries.AddRange(entry1, entry2, entry3, entry4);
            _dbContext.FxConversions.Add(fxConversion);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);

            return (transaction, fxConversion);
        }
        catch
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<LedgerTransaction> ReverseTransactionAsync(
        Guid originalTransactionId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reversal reason is required.", nameof(reason));

        using var dbTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var originalTxn = await _dbContext.LedgerTransactions.FirstOrDefaultAsync(t => t.Id == originalTransactionId, cancellationToken)
                ?? throw new InvalidOperationException($"Transaction '{originalTransactionId}' not found.");

            // Mark original transaction as reversed (throws if already reversed or not completed)
            originalTxn.MarkReversed(DateTime.UtcNow);

            var originalEntries = await _dbContext.LedgerEntries
                .Where(e => e.LedgerTransactionId == originalTransactionId)
                .OrderBy(e => e.Sequence)
                .ToListAsync(cancellationToken);

            if (originalEntries.Count == 0)
            {
                throw new InvalidOperationException($"No ledger entries found for transaction '{originalTransactionId}'.");
            }

            var reversalTxn = new LedgerTransaction(
                LedgerTransactionType.Reversal,
                reference: $"REV-{originalTxn.Reference}",
                description: $"Reversal of {originalTxn.Reference}: {reason}");

            reversalTxn.Complete(DateTime.UtcNow);

            var reversalEntries = new List<LedgerEntry>();
            int seq = 1;

            foreach (var entry in originalEntries)
            {
                var account = await _dbContext.LedgerAccounts.FirstOrDefaultAsync(l => l.Id == entry.LedgerAccountId, cancellationToken)
                    ?? throw new InvalidOperationException($"Ledger account '{entry.LedgerAccountId}' not found.");

                // Reverse entry direction
                var reversalDirection = entry.Direction == LedgerEntryDirection.Debit
                    ? LedgerEntryDirection.Credit
                    : LedgerEntryDirection.Debit;

                // Adjust wallet balance if customer wallet
                if (account.WalletId.HasValue)
                {
                    var wallet = _dbContext.Database.IsNpgsql()
                        ? await _dbContext.Wallets.FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", account.WalletId.Value).FirstOrDefaultAsync(cancellationToken)
                        : await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == account.WalletId.Value, cancellationToken);
                    if (wallet == null)
                        throw new InvalidOperationException($"Wallet '{account.WalletId.Value}' not found.");

                    if (reversalDirection == LedgerEntryDirection.Credit)
                    {
                        wallet.Credit(entry.Amount);
                    }
                    else
                    {
                        wallet.Debit(entry.Amount);
                    }
                }

                reversalEntries.Add(new LedgerEntry(
                    reversalTxn.Id,
                    entry.LedgerAccountId,
                    reversalDirection,
                    entry.Amount,
                    entry.Currency,
                    seq++));
            }

            _dbContext.LedgerTransactions.Add(reversalTxn);
            _dbContext.LedgerEntries.AddRange(reversalEntries);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);

            return reversalTxn;
        }
        catch
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<LedgerAccount> GetOrCreatePlatformFeeAccountAsync(Currency currency, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.AccountType == LedgerAccountType.FeeRevenue && l.Currency == currency, cancellationToken)
            ?? _dbContext.LedgerAccounts.Local
                .FirstOrDefault(l => l.AccountType == LedgerAccountType.FeeRevenue && l.Currency == currency);

        if (existing != null)
            return existing;

        var accountName = $"{currency} PLATFORM FEE";
        var account = LedgerAccount.CreateSystemAccount(accountName, currency, LedgerAccountType.FeeRevenue);
        _dbContext.LedgerAccounts.Add(account);

        if (_dbContext.Database.CurrentTransaction == null)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                var concurrentAccount = await _dbContext.LedgerAccounts
                    .FirstOrDefaultAsync(l => l.AccountType == LedgerAccountType.FeeRevenue && l.Currency == currency, cancellationToken);
                if (concurrentAccount != null)
                {
                    return concurrentAccount;
                }
                throw;
            }
        }

        return account;
    }

    /// <inheritdoc/>
    public async Task<LedgerTransaction> PostPeerTransferCoreAsync(
        Guid senderWalletId,
        Guid recipientWalletId,
        Guid platformFeeAccountId,
        decimal transferAmount,
        decimal feeAmount,
        Currency currency,
        string reference,
        string? idempotencyKey,
        string? description,
        CancellationToken cancellationToken = default)
    {
        if (transferAmount <= 0)
            throw new ArgumentException("Transfer amount must be positive.", nameof(transferAmount));
        if (feeAmount < 0)
            throw new ArgumentException("Fee amount cannot be negative.", nameof(feeAmount));
        if (senderWalletId == recipientWalletId)
            throw new ArgumentException("Sender and recipient wallets must be different.", nameof(recipientWalletId));

        // Deterministic lock order: lower GUID first to prevent deadlocks
        var firstId = senderWalletId.CompareTo(recipientWalletId) < 0 ? senderWalletId : recipientWalletId;
        var secondId = senderWalletId.CompareTo(recipientWalletId) < 0 ? recipientWalletId : senderWalletId;

        var firstWallet = _dbContext.Database.IsNpgsql()
            ? await _dbContext.Wallets.FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", firstId).FirstOrDefaultAsync(cancellationToken)
            : await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == firstId, cancellationToken);
        if (firstWallet == null)
            throw new InvalidOperationException($"Wallet '{firstId}' not found.");

        var secondWallet = _dbContext.Database.IsNpgsql()
            ? await _dbContext.Wallets.FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", secondId).FirstOrDefaultAsync(cancellationToken)
            : await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == secondId, cancellationToken);
        if (secondWallet == null)
            throw new InvalidOperationException($"Wallet '{secondId}' not found.");

        var senderWallet = senderWalletId == firstId ? firstWallet : secondWallet;
        var recipientWallet = recipientWalletId == firstId ? firstWallet : secondWallet;

        // Re-validate wallet statuses after locking (TOCTOU protection)
        if (senderWallet.Status != Domain.Finance.Enums.WalletStatus.Active)
            throw new InvalidOperationException($"Sender wallet is not active (status: {senderWallet.Status}).");
        if (recipientWallet.Status != Domain.Finance.Enums.WalletStatus.Active)
            throw new InvalidOperationException($"Recipient wallet is not active (status: {recipientWallet.Status}).");

        // Re-validate currency
        if (senderWallet.Currency != currency)
            throw new InvalidOperationException($"Sender wallet currency ({senderWallet.Currency}) does not match transaction currency ({currency}).");
        if (recipientWallet.Currency != currency)
            throw new InvalidOperationException($"Recipient wallet currency ({recipientWallet.Currency}) does not match transaction currency ({currency}).");

        // Re-validate balance after locking (TOCTOU protection)
        var totalDebit = transferAmount + feeAmount;
        if (senderWallet.AvailableBalance < totalDebit)
            throw new InvalidOperationException(
                $"Insufficient funds after lock. Required: {totalDebit}, Available: {senderWallet.AvailableBalance}.");

        // Retrieve ledger accounts
        var senderAccount = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.WalletId == senderWalletId, cancellationToken)
            ?? _dbContext.LedgerAccounts.Local
                .FirstOrDefault(l => l.WalletId == senderWalletId)
            ?? throw new InvalidOperationException($"Ledger account for sender wallet '{senderWalletId}' not found.");

        var recipientAccount = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.WalletId == recipientWalletId, cancellationToken)
            ?? _dbContext.LedgerAccounts.Local
                .FirstOrDefault(l => l.WalletId == recipientWalletId)
            ?? throw new InvalidOperationException($"Ledger account for recipient wallet '{recipientWalletId}' not found.");

        var feeAccount = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.Id == platformFeeAccountId, cancellationToken)
            ?? _dbContext.LedgerAccounts.Local
                .FirstOrDefault(l => l.Id == platformFeeAccountId)
            ?? throw new InvalidOperationException($"Platform fee ledger account '{platformFeeAccountId}' not found.");

        // Materialize wallet balances
        senderWallet.Debit(totalDebit);
        recipientWallet.Credit(transferAmount);

        // Build ledger transaction
        var transaction = new LedgerTransaction(LedgerTransactionType.PeerTransfer, reference, idempotencyKey, description);
        transaction.Complete(DateTime.UtcNow);

        // Double-entry ledger entries
        var entries = new List<LedgerEntry>
        {
            new LedgerEntry(transaction.Id, senderAccount.Id, LedgerEntryDirection.Debit, totalDebit, currency, sequence: 1),
            new LedgerEntry(transaction.Id, recipientAccount.Id, LedgerEntryDirection.Credit, transferAmount, currency, sequence: 2)
        };

        // Only create fee entry when there is a non-zero fee (FREE policy produces 0)
        if (feeAmount > 0)
        {
            entries.Add(new LedgerEntry(transaction.Id, feeAccount.Id, LedgerEntryDirection.Credit, feeAmount, currency, sequence: 3));
        }

        _dbContext.LedgerTransactions.Add(transaction);
        _dbContext.LedgerEntries.AddRange(entries);

        // SaveChanges within the ambient transaction — no commit here
        await _dbContext.SaveChangesAsync(cancellationToken);

        return transaction;
    }

    /// <inheritdoc/>
    public async Task<LedgerAccount> GetOrCreateBankTransferClearingAccountAsync(Currency currency, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.AccountType == LedgerAccountType.PlatformClearing && l.Currency == currency, cancellationToken)
            ?? _dbContext.LedgerAccounts.Local
                .FirstOrDefault(l => l.AccountType == LedgerAccountType.PlatformClearing && l.Currency == currency);

        if (existing != null)
            return existing;

        var accountName = $"{currency} BANK TRANSFER CLEARING";
        var account = LedgerAccount.CreateSystemAccount(accountName, currency, LedgerAccountType.PlatformClearing);
        _dbContext.LedgerAccounts.Add(account);

        if (_dbContext.Database.CurrentTransaction == null)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                var concurrentAccount = await _dbContext.LedgerAccounts
                    .FirstOrDefaultAsync(l => l.AccountType == LedgerAccountType.PlatformClearing && l.Currency == currency, cancellationToken);
                if (concurrentAccount != null)
                {
                    return concurrentAccount;
                }
                throw;
            }
        }

        return account;
    }

    /// <inheritdoc/>
    public async Task<(LedgerTransaction Transaction, BankTransfer Transfer)> PostBankTransferDebitCoreAsync(
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
        CancellationToken cancellationToken = default)
    {
        if (transferAmount <= 0)
            throw new ArgumentException("Transfer amount must be positive.", nameof(transferAmount));
        if (feeAmount < 0)
            throw new ArgumentException("Fee amount cannot be negative.", nameof(feeAmount));
        if (string.IsNullOrWhiteSpace(destinationBankCode))
            throw new ArgumentException("DestinationBankCode is required.", nameof(destinationBankCode));
        if (string.IsNullOrWhiteSpace(destinationAccountNumber))
            throw new ArgumentException("DestinationAccountNumber is required.", nameof(destinationAccountNumber));

        // Lock sender wallet with row-level lock
        var senderWallet = _dbContext.Database.IsNpgsql()
            ? await _dbContext.Wallets.FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", senderWalletId).FirstOrDefaultAsync(cancellationToken)
            : await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == senderWalletId, cancellationToken);

        if (senderWallet == null)
            throw new InvalidOperationException($"Sender wallet '{senderWalletId}' not found.");

        // Re-validate wallet status after acquiring lock
        if (senderWallet.Status != Domain.Finance.Enums.WalletStatus.Active)
            throw new InvalidOperationException($"Sender wallet is not active (status: {senderWallet.Status}).");

        // Re-validate currency
        if (senderWallet.Currency != currency)
            throw new InvalidOperationException($"Sender wallet currency ({senderWallet.Currency}) does not match transaction currency ({currency}).");

        // Re-validate available balance
        var totalDebit = transferAmount + feeAmount;
        if (senderWallet.AvailableBalance < totalDebit)
            throw new InvalidOperationException($"Insufficient funds after lock. Required: {totalDebit}, Available: {senderWallet.AvailableBalance}.");

        // Retrieve ledger accounts
        var senderAccount = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.WalletId == senderWalletId, cancellationToken)
            ?? _dbContext.LedgerAccounts.Local
                .FirstOrDefault(l => l.WalletId == senderWalletId)
            ?? throw new InvalidOperationException($"Ledger account for sender wallet '{senderWalletId}' not found.");

        var clearingAccount = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.Id == clearingAccountId, cancellationToken)
            ?? _dbContext.LedgerAccounts.Local
                .FirstOrDefault(l => l.Id == clearingAccountId)
            ?? throw new InvalidOperationException($"Bank transfer clearing ledger account '{clearingAccountId}' not found.");

        var feeAccount = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.Id == platformFeeAccountId, cancellationToken)
            ?? _dbContext.LedgerAccounts.Local
                .FirstOrDefault(l => l.Id == platformFeeAccountId)
            ?? throw new InvalidOperationException($"Platform fee ledger account '{platformFeeAccountId}' not found.");

        // Materialize sender wallet balance debit
        senderWallet.Debit(totalDebit);

        // Build central ledger transaction in PENDING status
        var transaction = new LedgerTransaction(LedgerTransactionType.BankTransfer, reference, idempotencyKey, description);

        // Double-entry ledger entries:
        // DEBIT  Sender Ledger Account               (Amount + Fee)
        // CREDIT Bank Transfer Clearing Account       (Amount)
        // CREDIT Platform Fee Revenue Account         (Fee, if > 0)
        var entries = new List<LedgerEntry>
        {
            new LedgerEntry(transaction.Id, senderAccount.Id, LedgerEntryDirection.Debit, totalDebit, currency, sequence: 1),
            new LedgerEntry(transaction.Id, clearingAccount.Id, LedgerEntryDirection.Credit, transferAmount, currency, sequence: 2)
        };

        if (feeAmount > 0)
        {
            entries.Add(new LedgerEntry(transaction.Id, feeAccount.Id, LedgerEntryDirection.Credit, feeAmount, currency, sequence: 3));
        }

        // Build BankTransfer aggregate
        var bankTransfer = BankTransfer.CreatePending(
            ledgerTransactionId: transaction.Id,
            senderWalletId: senderWalletId,
            destinationBankCode: destinationBankCode,
            destinationAccountNumber: destinationAccountNumber,
            destinationAccountName: destinationAccountName,
            amount: transferAmount,
            currency: currency,
            feeAmount: feeAmount,
            feePolicyId: feePolicyId,
            feePolicyVersion: feePolicyVersion,
            reference: reference);

        _dbContext.LedgerTransactions.Add(transaction);
        _dbContext.LedgerEntries.AddRange(entries);
        _dbContext.BankTransfers.Add(bankTransfer);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return (transaction, bankTransfer);
    }

    /// <inheritdoc/>
    public async Task<LedgerTransaction> PostBankTransferReversalCoreAsync(
        Guid bankTransferId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reversal reason is required.", nameof(reason));

        var transfer = await _dbContext.BankTransfers
            .FirstOrDefaultAsync(t => t.Id == bankTransferId, cancellationToken)
            ?? throw new InvalidOperationException($"Bank transfer '{bankTransferId}' not found.");

        if (transfer.Status == BankTransferStatus.Completed)
            throw new InvalidOperationException("Cannot reverse a completed bank transfer.");
        if (transfer.Status == BankTransferStatus.Failed)
            throw new InvalidOperationException("Bank transfer has already been marked failed and reversed.");

        // Lock sender wallet
        var senderWallet = _dbContext.Database.IsNpgsql()
            ? await _dbContext.Wallets.FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", transfer.SenderWalletId).FirstOrDefaultAsync(cancellationToken)
            : await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == transfer.SenderWalletId, cancellationToken);

        if (senderWallet == null)
            throw new InvalidOperationException($"Sender wallet '{transfer.SenderWalletId}' not found.");

        // Fetch original transaction and entries
        var originalTxn = await _dbContext.LedgerTransactions
            .FirstOrDefaultAsync(t => t.Id == transfer.LedgerTransactionId, cancellationToken)
            ?? throw new InvalidOperationException($"Original ledger transaction '{transfer.LedgerTransactionId}' not found.");

        var originalEntries = await _dbContext.LedgerEntries
            .Where(e => e.LedgerTransactionId == transfer.LedgerTransactionId)
            .OrderBy(e => e.Sequence)
            .ToListAsync(cancellationToken);

        if (originalEntries.Count == 0)
            throw new InvalidOperationException($"No ledger entries found for transaction '{transfer.LedgerTransactionId}'.");

        // Restore sender wallet balance
        senderWallet.Credit(transfer.TotalDebited);

        // Mark transfer FAILED
        transfer.MarkFailed(reason);

        // Build reversal transaction
        var reversalReference = $"REV-{transfer.Reference}";
        var reversalTxn = new LedgerTransaction(
            LedgerTransactionType.Reversal,
            reversalReference,
            idempotencyKey: null,
            description: $"Reversal of bank transfer {transfer.Reference}: {reason}");
        reversalTxn.Complete(DateTime.UtcNow);

        // Build offsetting entries (reversing debit and credit sides)
        var reversalEntries = new List<LedgerEntry>();
        var seq = 1;
        foreach (var entry in originalEntries)
        {
            var reversalDirection = entry.Direction == LedgerEntryDirection.Debit
                ? LedgerEntryDirection.Credit
                : LedgerEntryDirection.Debit;

            reversalEntries.Add(new LedgerEntry(
                reversalTxn.Id,
                entry.LedgerAccountId,
                reversalDirection,
                entry.Amount,
                entry.Currency,
                seq++));
        }

        _dbContext.LedgerTransactions.Add(reversalTxn);
        _dbContext.LedgerEntries.AddRange(reversalEntries);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return reversalTxn;
    }

    /// <inheritdoc/>
    public async Task<LedgerAccount> GetOrCreateInboundClearingAccountAsync(Currency currency, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.AccountType == LedgerAccountType.PlatformClearing && l.Currency == currency && l.AccountName.Contains("INBOUND"), cancellationToken)
            ?? _dbContext.LedgerAccounts.Local
                .FirstOrDefault(l => l.AccountType == LedgerAccountType.PlatformClearing && l.Currency == currency && l.AccountName.Contains("INBOUND"));

        if (existing != null)
            return existing;

        var accountName = $"{currency} INBOUND FUNDING CLEARING";
        var account = LedgerAccount.CreateSystemAccount(accountName, currency, LedgerAccountType.PlatformClearing);
        _dbContext.LedgerAccounts.Add(account);

        if (_dbContext.Database.CurrentTransaction == null)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                var concurrentAccount = await _dbContext.LedgerAccounts
                    .FirstOrDefaultAsync(l => l.AccountType == LedgerAccountType.PlatformClearing && l.Currency == currency && l.AccountName.Contains("INBOUND"), cancellationToken);
                if (concurrentAccount != null)
                {
                    return concurrentAccount;
                }
                throw;
            }
        }

        return account;
    }

    /// <inheritdoc/>
    public async Task<(LedgerTransaction Transaction, FundingTransaction Funding)> PostInboundFundingCreditCoreAsync(
        Guid walletId,
        Guid? virtualAccountId,
        decimal amount,
        Currency currency,
        PaymentProvider provider,
        string providerTransactionReference,
        FundingChannel channel,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            throw new ArgumentException("Funding amount must be positive.", nameof(amount));
        if (string.IsNullOrWhiteSpace(providerTransactionReference))
            throw new ArgumentException("ProviderTransactionReference is required.", nameof(providerTransactionReference));

        // Lock recipient wallet with row-level lock
        var recipientWallet = _dbContext.Database.IsNpgsql()
            ? await _dbContext.Wallets.FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", walletId).FirstOrDefaultAsync(cancellationToken)
            : await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == walletId, cancellationToken);

        if (recipientWallet == null)
            throw new InvalidOperationException($"Recipient wallet '{walletId}' not found.");
        if (recipientWallet.Status != WalletStatus.Active)
            throw new InvalidOperationException($"Recipient wallet '{walletId}' is not active.");
        if (recipientWallet.Currency != currency)
            throw new InvalidOperationException($"Recipient wallet currency '{recipientWallet.Currency}' does not match funding currency '{currency}'.");

        // Load or create funding transaction
        var fundingTx = await _dbContext.FundingTransactions
            .FirstOrDefaultAsync(f => f.Provider == provider && f.ProviderTransactionReference == providerTransactionReference, cancellationToken)
            ?? _dbContext.FundingTransactions.Local
                .FirstOrDefault(f => f.Provider == provider && f.ProviderTransactionReference == providerTransactionReference);

        if (fundingTx == null)
        {
            fundingTx = FundingTransaction.Create(
                walletId: walletId,
                virtualAccountId: virtualAccountId,
                provider: provider,
                providerTransactionReference: providerTransactionReference,
                fundingChannel: channel,
                amount: amount,
                currency: currency);
            _dbContext.FundingTransactions.Add(fundingTx);
        }

        if (fundingTx.Status == FundingTransactionStatus.Completed && fundingTx.LedgerTransactionId.HasValue)
        {
            // Already credited idempotently
            var existingTxn = await _dbContext.LedgerTransactions.FindAsync(new object[] { fundingTx.LedgerTransactionId.Value }, cancellationToken);
            return (existingTxn!, fundingTx);
        }

        // Get clearing account and customer wallet ledger account
        var clearingAccount = await GetOrCreateInboundClearingAccountAsync(currency, cancellationToken);
        var customerAccount = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.WalletId == walletId, cancellationToken)
            ?? _dbContext.LedgerAccounts.Local
                .FirstOrDefault(l => l.WalletId == walletId)
            ?? throw new InvalidOperationException($"Ledger account for wallet '{walletId}' not found.");

        // Credit recipient wallet available balance
        recipientWallet.Credit(amount);

        // Build LedgerTransaction
        var txnType = channel == FundingChannel.VirtualAccount
            ? LedgerTransactionType.VirtualAccountDeposit
            : LedgerTransactionType.CardFunding;

        var reference = $"FND-{providerTransactionReference}";
        var transaction = new LedgerTransaction(
            txnType,
            reference,
            idempotencyKey: providerTransactionReference,
            description: description ?? $"Inbound {channel} deposit via {provider} ({providerTransactionReference})");
        transaction.Complete(DateTime.UtcNow);

        // Double-entry entries:
        // DEBIT  Inbound Clearing Account  (Amount)
        // CREDIT Customer Wallet Account   (Amount)
        var entries = new List<LedgerEntry>
        {
            new(transaction.Id, clearingAccount.Id, LedgerEntryDirection.Debit, amount, currency, 1),
            new(transaction.Id, customerAccount.Id, LedgerEntryDirection.Credit, amount, currency, 2)
        };

        fundingTx.MarkCompleted(transaction.Id);

        _dbContext.LedgerTransactions.Add(transaction);
        _dbContext.LedgerEntries.AddRange(entries);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (transaction, fundingTx);
    }

    /// <inheritdoc/>
    public async Task<LedgerTransaction> PostPayrollDisbursementCoreAsync(
        Guid organizationWalletId,
        Guid employeeWalletId,
        decimal amount,
        Currency currency,
        string reference,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            throw new ArgumentException("Disbursement amount must be positive.", nameof(amount));
        if (organizationWalletId == Guid.Empty)
            throw new ArgumentException("OrganizationWalletId is required.", nameof(organizationWalletId));
        if (employeeWalletId == Guid.Empty)
            throw new ArgumentException("EmployeeWalletId is required.", nameof(employeeWalletId));

        currency.EnsureTransactionalV1();

        // Consistent lock order to prevent deadlocks
        var firstId = organizationWalletId.CompareTo(employeeWalletId) < 0 ? organizationWalletId : employeeWalletId;
        var secondId = firstId == organizationWalletId ? employeeWalletId : organizationWalletId;

        var firstWallet = _dbContext.Database.IsNpgsql()
            ? await _dbContext.Wallets.FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", firstId).FirstOrDefaultAsync(cancellationToken)
            : await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == firstId, cancellationToken);

        var secondWallet = _dbContext.Database.IsNpgsql()
            ? await _dbContext.Wallets.FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", secondId).FirstOrDefaultAsync(cancellationToken)
            : await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == secondId, cancellationToken);

        if (firstWallet == null)
            throw new InvalidOperationException($"Wallet '{firstId}' not found.");
        if (secondWallet == null)
            throw new InvalidOperationException($"Wallet '{secondId}' not found.");

        var orgWallet = firstWallet.Id == organizationWalletId ? firstWallet : secondWallet;
        var empWallet = firstWallet.Id == employeeWalletId ? firstWallet : secondWallet;

        if (orgWallet.Currency != currency)
            throw new InvalidOperationException($"Organization wallet currency '{orgWallet.Currency}' does not match payroll currency '{currency}'.");
        if (empWallet.Currency != currency)
            throw new InvalidOperationException($"Employee wallet currency '{empWallet.Currency}' does not match payroll currency '{currency}'.");

        if (orgWallet.Status != WalletStatus.Active)
            throw new InvalidOperationException($"Organization wallet is {orgWallet.Status}.");
        if (empWallet.Status != WalletStatus.Active)
            throw new InvalidOperationException($"Employee wallet is {empWallet.Status}.");

        if (orgWallet.AvailableBalance < amount)
            throw new InvalidOperationException($"Insufficient organization wallet balance. Required: {amount:F2} {currency}, Available: {orgWallet.AvailableBalance:F2} {currency}.");

        // Mutate wallet balances
        orgWallet.Debit(amount);
        empWallet.Credit(amount);

        // Get or create Ledger accounts
        var orgAccount = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.WalletId == organizationWalletId, cancellationToken)
            ?? _dbContext.LedgerAccounts.Local.FirstOrDefault(l => l.WalletId == organizationWalletId)
            ?? LedgerAccount.CreateWalletAccount(organizationWalletId, $"ORG WALLET {organizationWalletId:N}", currency);

        if (_dbContext.Entry(orgAccount).State == EntityState.Detached)
            _dbContext.LedgerAccounts.Add(orgAccount);

        var empAccount = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.WalletId == employeeWalletId, cancellationToken)
            ?? _dbContext.LedgerAccounts.Local.FirstOrDefault(l => l.WalletId == employeeWalletId)
            ?? LedgerAccount.CreateWalletAccount(employeeWalletId, $"EMP WALLET {employeeWalletId:N}", currency);

        if (_dbContext.Entry(empAccount).State == EntityState.Detached)
            _dbContext.LedgerAccounts.Add(empAccount);

        // Build LedgerTransaction
        var transaction = new LedgerTransaction(
            LedgerTransactionType.Payroll,
            reference,
            idempotencyKey: reference,
            description: description ?? $"Payroll disbursement {reference}");
        transaction.Complete(DateTime.UtcNow);

        // Double-entry entries:
        // DEBIT  Organization Wallet Account  (Amount)
        // CREDIT Employee Wallet Account      (Amount)
        var entries = new List<LedgerEntry>
        {
            new(transaction.Id, orgAccount.Id, LedgerEntryDirection.Debit, amount, currency, 1),
            new(transaction.Id, empAccount.Id, LedgerEntryDirection.Credit, amount, currency, 2)
        };

        _dbContext.LedgerTransactions.Add(transaction);
        _dbContext.LedgerEntries.AddRange(entries);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return transaction;
    }

    /// <inheritdoc/>
    public async Task<LedgerAccount> GetOrCreateLoanFundAccountAsync(Currency currency, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.AccountType == LedgerAccountType.PlatformClearing && l.Currency == currency && l.AccountName.Contains("LOAN FUND"), cancellationToken)
            ?? _dbContext.LedgerAccounts.Local
                .FirstOrDefault(l => l.AccountType == LedgerAccountType.PlatformClearing && l.Currency == currency && l.AccountName.Contains("LOAN FUND"));

        if (existing != null)
            return existing;

        var accountName = $"{currency} PLATFORM LOAN FUND";
        var account = LedgerAccount.CreateSystemAccount(accountName, currency, LedgerAccountType.PlatformClearing);
        _dbContext.LedgerAccounts.Add(account);

        if (_dbContext.Database.CurrentTransaction == null)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                var concurrent = await _dbContext.LedgerAccounts
                    .FirstOrDefaultAsync(l => l.AccountType == LedgerAccountType.PlatformClearing && l.Currency == currency && l.AccountName.Contains("LOAN FUND"), cancellationToken);
                if (concurrent != null)
                    return concurrent;
                throw;
            }
        }

        return account;
    }

    /// <inheritdoc/>
    public async Task<LedgerAccount> GetOrCreateLoanReceivableAccountAsync(Currency currency, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.AccountType == LedgerAccountType.PlatformClearing && l.Currency == currency && l.AccountName.Contains("LOAN RECEIVABLE"), cancellationToken)
            ?? _dbContext.LedgerAccounts.Local
                .FirstOrDefault(l => l.AccountType == LedgerAccountType.PlatformClearing && l.Currency == currency && l.AccountName.Contains("LOAN RECEIVABLE"));

        if (existing != null)
            return existing;

        var accountName = $"{currency} PLATFORM LOAN RECEIVABLE";
        var account = LedgerAccount.CreateSystemAccount(accountName, currency, LedgerAccountType.PlatformClearing);
        _dbContext.LedgerAccounts.Add(account);

        if (_dbContext.Database.CurrentTransaction == null)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                var concurrent = await _dbContext.LedgerAccounts
                    .FirstOrDefaultAsync(l => l.AccountType == LedgerAccountType.PlatformClearing && l.Currency == currency && l.AccountName.Contains("LOAN RECEIVABLE"), cancellationToken);
                if (concurrent != null)
                    return concurrent;
                throw;
            }
        }

        return account;
    }

    /// <inheritdoc/>
    public async Task<LedgerTransaction> PostLoanDisbursementCoreAsync(
        Guid employeeWalletId,
        decimal amount,
        Currency currency,
        string reference,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            throw new ArgumentException("Disbursement amount must be positive.", nameof(amount));

        // Row-level lock on employee wallet
        var empWallet = _dbContext.Database.IsNpgsql()
            ? await _dbContext.Wallets.FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", employeeWalletId).FirstOrDefaultAsync(cancellationToken)
            : await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == employeeWalletId, cancellationToken);

        if (empWallet == null)
            throw new InvalidOperationException($"Employee wallet '{employeeWalletId}' not found.");
        if (empWallet.Currency != currency)
            throw new InvalidOperationException($"Employee wallet currency '{empWallet.Currency}' does not match loan currency '{currency}'.");
        if (empWallet.Status != WalletStatus.Active)
            throw new InvalidOperationException($"Employee wallet is {empWallet.Status}.");

        // Mutate employee wallet balance
        empWallet.Credit(amount);

        // Resolve system loan fund account and employee ledger account
        var loanFundAccount = await GetOrCreateLoanFundAccountAsync(currency, cancellationToken);

        var empAccount = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.WalletId == employeeWalletId, cancellationToken)
            ?? _dbContext.LedgerAccounts.Local.FirstOrDefault(l => l.WalletId == employeeWalletId)
            ?? LedgerAccount.CreateWalletAccount(employeeWalletId, $"EMP WALLET {employeeWalletId:N}", currency);

        if (_dbContext.Entry(empAccount).State == EntityState.Detached)
            _dbContext.LedgerAccounts.Add(empAccount);

        var transaction = new LedgerTransaction(
            LedgerTransactionType.LoanDisbursement,
            reference,
            idempotencyKey: reference,
            description: description ?? $"Loan disbursement {reference}");
        transaction.Complete(DateTime.UtcNow);

        // Double-entry entries:
        // DEBIT  Platform Loan Fund Account (amount)
        // CREDIT Employee Wallet Account     (amount)
        var entries = new List<LedgerEntry>
        {
            new(transaction.Id, loanFundAccount.Id, LedgerEntryDirection.Debit, amount, currency, 1),
            new(transaction.Id, empAccount.Id, LedgerEntryDirection.Credit, amount, currency, 2)
        };

        _dbContext.LedgerTransactions.Add(transaction);
        _dbContext.LedgerEntries.AddRange(entries);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return transaction;
    }

    /// <inheritdoc/>
    public async Task<LedgerTransaction> PostLoanRepaymentCoreAsync(
        Guid employeeWalletId,
        decimal amount,
        Currency currency,
        string reference,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            throw new ArgumentException("Repayment amount must be positive.", nameof(amount));

        // Row-level lock on employee wallet
        var empWallet = _dbContext.Database.IsNpgsql()
            ? await _dbContext.Wallets.FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", employeeWalletId).FirstOrDefaultAsync(cancellationToken)
            : await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == employeeWalletId, cancellationToken);

        if (empWallet == null)
            throw new InvalidOperationException($"Employee wallet '{employeeWalletId}' not found.");
        if (empWallet.Currency != currency)
            throw new InvalidOperationException($"Employee wallet currency '{empWallet.Currency}' does not match loan currency '{currency}'.");
        if (empWallet.Status != WalletStatus.Active)
            throw new InvalidOperationException($"Employee wallet is {empWallet.Status}.");
        if (empWallet.AvailableBalance < amount)
            throw new InvalidOperationException($"Insufficient employee wallet balance for loan repayment. Required: {amount:F2}, Available: {empWallet.AvailableBalance:F2}.");

        // Mutate employee wallet balance
        empWallet.Debit(amount);

        // Resolve system loan receivable account and employee ledger account
        var loanReceivableAccount = await GetOrCreateLoanReceivableAccountAsync(currency, cancellationToken);

        var empAccount = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.WalletId == employeeWalletId, cancellationToken)
            ?? _dbContext.LedgerAccounts.Local.FirstOrDefault(l => l.WalletId == employeeWalletId)
            ?? LedgerAccount.CreateWalletAccount(employeeWalletId, $"EMP WALLET {employeeWalletId:N}", currency);

        if (_dbContext.Entry(empAccount).State == EntityState.Detached)
            _dbContext.LedgerAccounts.Add(empAccount);

        var transaction = new LedgerTransaction(
            LedgerTransactionType.LoanRepayment,
            reference,
            idempotencyKey: reference,
            description: description ?? $"Loan repayment {reference}");
        transaction.Complete(DateTime.UtcNow);

        // Double-entry entries:
        // DEBIT  Employee Wallet Account     (amount)
        // CREDIT Platform Loan Receivable     (amount)
        var entries = new List<LedgerEntry>
        {
            new(transaction.Id, empAccount.Id, LedgerEntryDirection.Debit, amount, currency, 1),
            new(transaction.Id, loanReceivableAccount.Id, LedgerEntryDirection.Credit, amount, currency, 2)
        };

        _dbContext.LedgerTransactions.Add(transaction);
        _dbContext.LedgerEntries.AddRange(entries);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return transaction;
    }

    /// <inheritdoc/>
    public async Task<LedgerAccount> GetOrCreateSavingsPoolAccountAsync(Currency currency, CancellationToken cancellationToken = default)
    {
        var accountName = $"{currency} PLATFORM SAVINGS POOL";
        var account = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.AccountName == accountName && l.Currency == currency, cancellationToken)
            ?? _dbContext.LedgerAccounts.Local
                .FirstOrDefault(l => l.AccountName == accountName && l.Currency == currency);

        if (account != null) return account;

        account = LedgerAccount.CreateSystemAccount(accountName, currency, LedgerAccountType.PlatformClearing);
        _dbContext.LedgerAccounts.Add(account);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return account;
    }

    /// <inheritdoc/>
    public async Task<LedgerAccount> GetOrCreateThriftPoolAccountAsync(Guid thriftGroupId, Currency currency, CancellationToken cancellationToken = default)
    {
        var accountName = $"THRIFT POOL {thriftGroupId:N} {currency}";
        var account = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.AccountName == accountName && l.Currency == currency, cancellationToken)
            ?? _dbContext.LedgerAccounts.Local
                .FirstOrDefault(l => l.AccountName == accountName && l.Currency == currency);

        if (account != null) return account;

        account = LedgerAccount.CreateSystemAccount(accountName, currency, LedgerAccountType.PlatformClearing);
        _dbContext.LedgerAccounts.Add(account);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return account;
    }

    /// <inheritdoc/>
    public async Task<LedgerTransaction> PostSavingsContributionCoreAsync(
        Guid userWalletId,
        decimal amount,
        Currency currency,
        string reference,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            throw new ArgumentException("Contribution amount must be positive.", nameof(amount));

        // Lock user wallet with row-level lock
        var userWallet = _dbContext.Database.IsNpgsql()
            ? await _dbContext.Wallets.FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", userWalletId).FirstOrDefaultAsync(cancellationToken)
            : await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == userWalletId, cancellationToken);

        if (userWallet == null)
            throw new InvalidOperationException($"User wallet '{userWalletId}' not found.");
        if (userWallet.Currency != currency)
            throw new InvalidOperationException($"User wallet currency '{userWallet.Currency}' does not match contribution currency '{currency}'.");
        if (userWallet.Status != WalletStatus.Active)
            throw new InvalidOperationException($"User wallet is {userWallet.Status}.");
        if (userWallet.AvailableBalance < amount)
            throw new InvalidOperationException($"Insufficient wallet balance for savings contribution. Required: {amount:F2}, Available: {userWallet.AvailableBalance:F2}.");

        // Mutate wallet balance
        userWallet.Debit(amount);

        // Resolve savings pool account and user ledger account
        var poolAccount = await GetOrCreateSavingsPoolAccountAsync(currency, cancellationToken);
        var userAccount = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.WalletId == userWalletId, cancellationToken)
            ?? _dbContext.LedgerAccounts.Local.FirstOrDefault(l => l.WalletId == userWalletId)
            ?? LedgerAccount.CreateWalletAccount(userWalletId, $"USER WALLET {userWalletId:N}", currency);

        if (_dbContext.Entry(userAccount).State == EntityState.Detached)
            _dbContext.LedgerAccounts.Add(userAccount);

        var transaction = new LedgerTransaction(
            LedgerTransactionType.SavingsContribution,
            reference,
            idempotencyKey: reference,
            description: description ?? $"Savings contribution {reference}");
        transaction.Complete(DateTime.UtcNow);

        // Double-entry entries:
        // DEBIT  User Wallet Account       (amount)
        // CREDIT Platform Savings Pool      (amount)
        var entries = new List<LedgerEntry>
        {
            new(transaction.Id, userAccount.Id, LedgerEntryDirection.Debit, amount, currency, 1),
            new(transaction.Id, poolAccount.Id, LedgerEntryDirection.Credit, amount, currency, 2)
        };

        _dbContext.LedgerTransactions.Add(transaction);
        _dbContext.LedgerEntries.AddRange(entries);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return transaction;
    }

    /// <inheritdoc/>
    public async Task<LedgerTransaction> PostSavingsWithdrawalCoreAsync(
        Guid userWalletId,
        decimal payoutAmount,
        Currency currency,
        string reference,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (payoutAmount <= 0)
            throw new ArgumentException("Payout amount must be positive.", nameof(payoutAmount));

        // Lock user wallet with row-level lock
        var userWallet = _dbContext.Database.IsNpgsql()
            ? await _dbContext.Wallets.FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", userWalletId).FirstOrDefaultAsync(cancellationToken)
            : await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == userWalletId, cancellationToken);

        if (userWallet == null)
            throw new InvalidOperationException($"User wallet '{userWalletId}' not found.");
        if (userWallet.Currency != currency)
            throw new InvalidOperationException($"User wallet currency '{userWallet.Currency}' does not match payout currency '{currency}'.");
        if (userWallet.Status != WalletStatus.Active)
            throw new InvalidOperationException($"User wallet is {userWallet.Status}.");

        // Credit user wallet
        userWallet.Credit(payoutAmount);

        // Resolve savings pool account and user ledger account
        var poolAccount = await GetOrCreateSavingsPoolAccountAsync(currency, cancellationToken);
        var userAccount = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.WalletId == userWalletId, cancellationToken)
            ?? _dbContext.LedgerAccounts.Local.FirstOrDefault(l => l.WalletId == userWalletId)
            ?? LedgerAccount.CreateWalletAccount(userWalletId, $"USER WALLET {userWalletId:N}", currency);

        if (_dbContext.Entry(userAccount).State == EntityState.Detached)
            _dbContext.LedgerAccounts.Add(userAccount);

        var transaction = new LedgerTransaction(
            LedgerTransactionType.SavingsWithdrawal,
            reference,
            idempotencyKey: reference,
            description: description ?? $"Savings withdrawal {reference}");
        transaction.Complete(DateTime.UtcNow);

        // Double-entry entries:
        // DEBIT  Platform Savings Pool      (payoutAmount)
        // CREDIT User Wallet Account         (payoutAmount)
        var entries = new List<LedgerEntry>
        {
            new(transaction.Id, poolAccount.Id, LedgerEntryDirection.Debit, payoutAmount, currency, 1),
            new(transaction.Id, userAccount.Id, LedgerEntryDirection.Credit, payoutAmount, currency, 2)
        };

        _dbContext.LedgerTransactions.Add(transaction);
        _dbContext.LedgerEntries.AddRange(entries);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return transaction;
    }

    /// <inheritdoc/>
    public async Task<LedgerTransaction> PostThriftContributionCoreAsync(
        Guid memberWalletId,
        Guid thriftGroupId,
        decimal amount,
        Currency currency,
        string reference,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            throw new ArgumentException("Contribution amount must be positive.", nameof(amount));

        // Lock member wallet with row-level lock
        var memberWallet = _dbContext.Database.IsNpgsql()
            ? await _dbContext.Wallets.FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", memberWalletId).FirstOrDefaultAsync(cancellationToken)
            : await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == memberWalletId, cancellationToken);

        if (memberWallet == null)
            throw new InvalidOperationException($"Member wallet '{memberWalletId}' not found.");
        if (memberWallet.Currency != currency)
            throw new InvalidOperationException($"Member wallet currency '{memberWallet.Currency}' does not match thrift currency '{currency}'.");
        if (memberWallet.Status != WalletStatus.Active)
            throw new InvalidOperationException($"Member wallet is {memberWallet.Status}.");
        if (memberWallet.AvailableBalance < amount)
            throw new InvalidOperationException($"Insufficient member wallet balance for thrift contribution. Required: {amount:F2}, Available: {memberWallet.AvailableBalance:F2}.");

        // Debit member wallet
        memberWallet.Debit(amount);

        // Resolve thrift pool account and member ledger account
        var thriftPoolAccount = await GetOrCreateThriftPoolAccountAsync(thriftGroupId, currency, cancellationToken);
        var memberAccount = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.WalletId == memberWalletId, cancellationToken)
            ?? _dbContext.LedgerAccounts.Local.FirstOrDefault(l => l.WalletId == memberWalletId)
            ?? LedgerAccount.CreateWalletAccount(memberWalletId, $"MEMBER WALLET {memberWalletId:N}", currency);

        if (_dbContext.Entry(memberAccount).State == EntityState.Detached)
            _dbContext.LedgerAccounts.Add(memberAccount);

        var transaction = new LedgerTransaction(
            LedgerTransactionType.ThriftContribution,
            reference,
            idempotencyKey: reference,
            description: description ?? $"Thrift contribution {reference}");
        transaction.Complete(DateTime.UtcNow);

        // Double-entry entries:
        // DEBIT  Member Wallet Account       (amount)
        // CREDIT Thrift Pool Account         (amount)
        var entries = new List<LedgerEntry>
        {
            new(transaction.Id, memberAccount.Id, LedgerEntryDirection.Debit, amount, currency, 1),
            new(transaction.Id, thriftPoolAccount.Id, LedgerEntryDirection.Credit, amount, currency, 2)
        };

        _dbContext.LedgerTransactions.Add(transaction);
        _dbContext.LedgerEntries.AddRange(entries);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return transaction;
    }

    /// <inheritdoc/>
    public async Task<LedgerTransaction> PostThriftPayoutCoreAsync(
        Guid beneficiaryWalletId,
        Guid thriftGroupId,
        decimal amount,
        Currency currency,
        string reference,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            throw new ArgumentException("Payout amount must be positive.", nameof(amount));

        // Lock beneficiary wallet with row-level lock
        var benWallet = _dbContext.Database.IsNpgsql()
            ? await _dbContext.Wallets.FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", beneficiaryWalletId).FirstOrDefaultAsync(cancellationToken)
            : await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == beneficiaryWalletId, cancellationToken);

        if (benWallet == null)
            throw new InvalidOperationException($"Beneficiary wallet '{beneficiaryWalletId}' not found.");
        if (benWallet.Currency != currency)
            throw new InvalidOperationException($"Beneficiary wallet currency '{benWallet.Currency}' does not match payout currency '{currency}'.");
        if (benWallet.Status != WalletStatus.Active)
            throw new InvalidOperationException($"Beneficiary wallet is {benWallet.Status}.");

        // Credit beneficiary wallet
        benWallet.Credit(amount);

        // Resolve thrift pool account and beneficiary ledger account
        var thriftPoolAccount = await GetOrCreateThriftPoolAccountAsync(thriftGroupId, currency, cancellationToken);
        var benAccount = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.WalletId == beneficiaryWalletId, cancellationToken)
            ?? _dbContext.LedgerAccounts.Local.FirstOrDefault(l => l.WalletId == beneficiaryWalletId)
            ?? LedgerAccount.CreateWalletAccount(beneficiaryWalletId, $"BEN WALLET {beneficiaryWalletId:N}", currency);

        if (_dbContext.Entry(benAccount).State == EntityState.Detached)
            _dbContext.LedgerAccounts.Add(benAccount);

        var transaction = new LedgerTransaction(
            LedgerTransactionType.ThriftPayout,
            reference,
            idempotencyKey: reference,
            description: description ?? $"Thrift payout {reference}");
        transaction.Complete(DateTime.UtcNow);

        // Double-entry entries:
        // DEBIT  Thrift Pool Account         (amount)
        // CREDIT Beneficiary Wallet Account  (amount)
        var entries = new List<LedgerEntry>
        {
            new(transaction.Id, thriftPoolAccount.Id, LedgerEntryDirection.Debit, amount, currency, 1),
            new(transaction.Id, benAccount.Id, LedgerEntryDirection.Credit, amount, currency, 2)
        };

        _dbContext.LedgerTransactions.Add(transaction);
        _dbContext.LedgerEntries.AddRange(entries);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return transaction;
    }

    /// <inheritdoc/>
    public async Task<LedgerTransaction> PostThriftReimbursementCoreAsync(
        Guid memberWalletId,
        Guid thriftGroupId,
        decimal amount,
        Currency currency,
        string reference,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            throw new ArgumentException("Reimbursement amount must be positive.", nameof(amount));

        // Lock member wallet with row-level lock
        var memberWallet = _dbContext.Database.IsNpgsql()
            ? await _dbContext.Wallets.FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", memberWalletId).FirstOrDefaultAsync(cancellationToken)
            : await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == memberWalletId, cancellationToken);

        if (memberWallet == null)
            throw new InvalidOperationException($"Member wallet '{memberWalletId}' not found.");
        if (memberWallet.Currency != currency)
            throw new InvalidOperationException($"Member wallet currency '{memberWallet.Currency}' does not match refund currency '{currency}'.");
        if (memberWallet.Status != WalletStatus.Active)
            throw new InvalidOperationException($"Member wallet is {memberWallet.Status}.");

        // Credit member wallet
        memberWallet.Credit(amount);

        // Resolve thrift pool account and member ledger account
        var thriftPoolAccount = await GetOrCreateThriftPoolAccountAsync(thriftGroupId, currency, cancellationToken);
        var memberAccount = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.WalletId == memberWalletId, cancellationToken)
            ?? _dbContext.LedgerAccounts.Local.FirstOrDefault(l => l.WalletId == memberWalletId)
            ?? LedgerAccount.CreateWalletAccount(memberWalletId, $"MEMBER WALLET {memberWalletId:N}", currency);

        if (_dbContext.Entry(memberAccount).State == EntityState.Detached)
            _dbContext.LedgerAccounts.Add(memberAccount);

        var transaction = new LedgerTransaction(
            LedgerTransactionType.Refund,
            reference,
            idempotencyKey: reference,
            description: description ?? $"Thrift reimbursement {reference}");
        transaction.Complete(DateTime.UtcNow);

        // Double-entry entries:
        // DEBIT  Thrift Pool Account         (amount)
        // CREDIT Member Wallet Account       (amount)
        var entries = new List<LedgerEntry>
        {
            new(transaction.Id, thriftPoolAccount.Id, LedgerEntryDirection.Debit, amount, currency, 1),
            new(transaction.Id, memberAccount.Id, LedgerEntryDirection.Credit, amount, currency, 2)
        };

        _dbContext.LedgerTransactions.Add(transaction);
        _dbContext.LedgerEntries.AddRange(entries);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return transaction;
    }

    /// <inheritdoc/>
    public async Task<LedgerAccount> GetOrCreateVasClearingAccountAsync(Currency currency, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.AccountType == LedgerAccountType.PlatformClearing && l.Currency == currency && l.AccountName.Contains("VAS CLEARING"), cancellationToken)
            ?? _dbContext.LedgerAccounts.Local
                .FirstOrDefault(l => l.AccountType == LedgerAccountType.PlatformClearing && l.Currency == currency && l.AccountName.Contains("VAS CLEARING"));

        if (existing != null)
        {
            return existing;
        }

        var accountName = $"{currency} VAS CLEARING";
        var account = LedgerAccount.CreateSystemAccount(accountName, currency, LedgerAccountType.PlatformClearing);
        _dbContext.LedgerAccounts.Add(account);

        if (_dbContext.Database.CurrentTransaction == null)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                var concurrentAccount = await _dbContext.LedgerAccounts
                    .FirstOrDefaultAsync(l => l.AccountType == LedgerAccountType.PlatformClearing && l.Currency == currency && l.AccountName.Contains("VAS CLEARING"), cancellationToken);
                if (concurrentAccount != null)
                {
                    return concurrentAccount;
                }
                throw;
            }
        }

        return account;
    }

    /// <inheritdoc/>
    public async Task<(LedgerTransaction Transaction, Domain.Vas.Entities.VasTransaction VasTransaction)> PostVasPurchaseDebitCoreAsync(
        Guid customerWalletId,
        Guid vasClearingAccountId,
        decimal amount,
        Currency currency,
        string userId,
        Guid? organizationId,
        string phoneNumber,
        Domain.Vas.Enums.VasNetwork network,
        Domain.Vas.Enums.VasType type,
        string? productCode,
        string? productName,
        string reference,
        string? idempotencyKey = null,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            throw new ArgumentException("Purchase amount must be positive.", nameof(amount));
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("PhoneNumber is required.", nameof(phoneNumber));

        // Lock customer wallet with row-level lock
        var customerWallet = _dbContext.Database.IsNpgsql()
            ? await _dbContext.Wallets.FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", customerWalletId).FirstOrDefaultAsync(cancellationToken)
            : await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == customerWalletId, cancellationToken);

        if (customerWallet == null)
            throw new InvalidOperationException($"Customer wallet '{customerWalletId}' not found.");
        if (customerWallet.Status != WalletStatus.Active)
            throw new InvalidOperationException($"Customer wallet is {customerWallet.Status}.");
        if (customerWallet.Currency != currency)
            throw new InvalidOperationException($"Customer wallet currency '{customerWallet.Currency}' does not match transaction currency '{currency}'.");
        if (customerWallet.AvailableBalance < amount)
            throw new InvalidOperationException($"Insufficient funds after lock. Required: {amount}, Available: {customerWallet.AvailableBalance}.");

        // Resolve customer ledger account and VAS clearing account
        var customerAccount = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.WalletId == customerWalletId, cancellationToken)
            ?? _dbContext.LedgerAccounts.Local.FirstOrDefault(l => l.WalletId == customerWalletId)
            ?? LedgerAccount.CreateWalletAccount(customerWalletId, $"CUSTOMER WALLET {customerWalletId:N}", currency);

        if (_dbContext.Entry(customerAccount).State == EntityState.Detached)
            _dbContext.LedgerAccounts.Add(customerAccount);

        var clearingAccount = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.Id == vasClearingAccountId, cancellationToken)
            ?? _dbContext.LedgerAccounts.Local.FirstOrDefault(l => l.Id == vasClearingAccountId)
            ?? throw new InvalidOperationException($"VAS clearing ledger account '{vasClearingAccountId}' not found.");

        // Debit customer wallet balance
        customerWallet.Debit(amount);

        // Build central ledger transaction in PENDING status
        var transaction = new LedgerTransaction(
            LedgerTransactionType.VasPurchase,
            reference,
            idempotencyKey,
            description ?? $"VAS {type} purchase {reference}");

        // Double-entry ledger entries:
        // DEBIT  Customer Wallet Account (amount)
        // CREDIT VAS Clearing Account    (amount)
        var entries = new List<LedgerEntry>
        {
            new(transaction.Id, customerAccount.Id, LedgerEntryDirection.Debit, amount, currency, sequence: 1),
            new(transaction.Id, clearingAccount.Id, LedgerEntryDirection.Credit, amount, currency, sequence: 2)
        };

        // Build VasTransaction aggregate
        var vasTxn = type == Domain.Vas.Enums.VasType.Airtime
            ? Domain.Vas.Entities.VasTransaction.CreateAirtime(
                reference: reference,
                userId: userId,
                organizationId: organizationId,
                walletId: customerWalletId,
                ledgerTransactionId: transaction.Id,
                phoneNumber: phoneNumber,
                network: network,
                amount: amount,
                currency: currency)
            : Domain.Vas.Entities.VasTransaction.CreateData(
                reference: reference,
                userId: userId,
                organizationId: organizationId,
                walletId: customerWalletId,
                ledgerTransactionId: transaction.Id,
                phoneNumber: phoneNumber,
                network: network,
                productCode: productCode ?? string.Empty,
                productName: productName,
                amount: amount,
                currency: currency);

        _dbContext.LedgerTransactions.Add(transaction);
        _dbContext.LedgerEntries.AddRange(entries);
        _dbContext.VasTransactions.Add(vasTxn);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (transaction, vasTxn);
    }

    /// <inheritdoc/>
    public async Task<LedgerTransaction> PostVasPurchaseReversalCoreAsync(
        Guid vasTransactionId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reversal reason is required.", nameof(reason));

        var vasTxn = await _dbContext.VasTransactions
            .FirstOrDefaultAsync(t => t.Id == vasTransactionId, cancellationToken)
            ?? throw new InvalidOperationException($"VAS transaction '{vasTransactionId}' not found.");

        if (vasTxn.Status == Domain.Vas.Enums.VasTransactionStatus.Succeeded)
            throw new InvalidOperationException("Cannot reverse a completed VAS transaction.");
        if (vasTxn.Status == Domain.Vas.Enums.VasTransactionStatus.Reversed)
            throw new InvalidOperationException("VAS transaction has already been marked failed and reversed.");

        // Lock customer wallet
        var customerWallet = _dbContext.Database.IsNpgsql()
            ? await _dbContext.Wallets.FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", vasTxn.WalletId).FirstOrDefaultAsync(cancellationToken)
            : await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == vasTxn.WalletId, cancellationToken);

        if (customerWallet == null)
            throw new InvalidOperationException($"Customer wallet '{vasTxn.WalletId}' not found.");

        // Restore customer wallet balance
        customerWallet.Credit(vasTxn.Amount);

        // Mark VAS transaction reversed
        vasTxn.MarkReversed(reason);

        // Build reversal transaction
        var reversalReference = $"REV-{vasTxn.Reference}";
        var reversalTxn = new LedgerTransaction(
            LedgerTransactionType.Reversal,
            reversalReference,
            idempotencyKey: reversalReference,
            description: $"Reversal of VAS purchase {vasTxn.Reference}: {reason}");

        reversalTxn.Complete(DateTime.UtcNow);

        // Fetch original entries
        var originalEntries = await _dbContext.LedgerEntries
            .Where(e => e.LedgerTransactionId == vasTxn.LedgerTransactionId)
            .OrderBy(e => e.Sequence)
            .ToListAsync(cancellationToken);

        var reversalEntries = new List<LedgerEntry>();
        int seq = 1;
        foreach (var original in originalEntries)
        {
            var oppositeDirection = original.Direction == LedgerEntryDirection.Debit
                ? LedgerEntryDirection.Credit
                : LedgerEntryDirection.Debit;

            reversalEntries.Add(new LedgerEntry(
                reversalTxn.Id,
                original.LedgerAccountId,
                oppositeDirection,
                original.Amount,
                original.Currency,
                seq++));
        }

        _dbContext.LedgerTransactions.Add(reversalTxn);
        _dbContext.LedgerEntries.AddRange(reversalEntries);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return reversalTxn;
    }
}


