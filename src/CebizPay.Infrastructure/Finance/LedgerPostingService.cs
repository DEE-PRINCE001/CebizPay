using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
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
            .FirstOrDefaultAsync(l => l.AccountType == LedgerAccountType.SystemSettlement && l.Currency == currency, cancellationToken);

        if (existing != null)
        {
            return existing;
        }

        var accountName = $"{currency} FX SETTLEMENT";
        var account = LedgerAccount.CreateSystemAccount(accountName, currency, LedgerAccountType.SystemSettlement);
        _dbContext.LedgerAccounts.Add(account);
        await _dbContext.SaveChangesAsync(cancellationToken);

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

            // Lock and update source wallet if customer wallet
            Wallet? sourceWallet = null;
            if (sourceAccount.WalletId.HasValue)
            {
                sourceWallet = await _dbContext.Wallets
                    .FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", sourceAccount.WalletId.Value)
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"Source wallet '{sourceAccount.WalletId.Value}' not found.");

                if (sourceWallet.Currency != currency)
                    throw new InvalidOperationException("Source wallet currency does not match transaction currency.");

                sourceWallet.Debit(amount);
            }

            // Lock and update target wallet if customer wallet
            Wallet? targetWallet = null;
            if (targetAccount.WalletId.HasValue)
            {
                targetWallet = await _dbContext.Wallets
                    .FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", targetAccount.WalletId.Value)
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"Target wallet '{targetAccount.WalletId.Value}' not found.");

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
            // Row-lock source and target wallets
            var sourceWallet = await _dbContext.Wallets
                .FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", sourceWalletId)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException($"Source wallet '{sourceWalletId}' not found.");

            var targetWallet = await _dbContext.Wallets
                .FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", targetWalletId)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException($"Target wallet '{targetWalletId}' not found.");

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
                    var wallet = await _dbContext.Wallets
                        .FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"Id\" = {0} FOR UPDATE", account.WalletId.Value)
                        .FirstOrDefaultAsync(cancellationToken)
                        ?? throw new InvalidOperationException($"Wallet '{account.WalletId.Value}' not found.");

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
}
