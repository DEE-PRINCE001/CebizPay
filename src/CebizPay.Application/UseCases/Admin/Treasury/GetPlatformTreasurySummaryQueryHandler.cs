using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Treasury;

/// <summary>
/// Handler for <see cref="GetPlatformTreasurySummaryQuery"/>.
/// Computes platform fee revenue pool balance, pending settlements, and reconciliation state.
/// </summary>
public sealed class GetPlatformTreasurySummaryQueryHandler : IRequestHandler<GetPlatformTreasurySummaryQuery, PlatformTreasurySummaryDto>
{
    private readonly IApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetPlatformTreasurySummaryQueryHandler"/>.
    /// </summary>
    public GetPlatformTreasurySummaryQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc/>
    public async Task<PlatformTreasurySummaryDto> Handle(GetPlatformTreasurySummaryQuery request, CancellationToken cancellationToken)
    {
        var targetCurrency = request.Currency;

        // 1. Resolve Platform Fee Revenue Account
        var feeAccount = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(l => l.AccountType == LedgerAccountType.FeeRevenue && l.Currency == targetCurrency, cancellationToken);

        decimal availableBalance = 0m;
        if (feeAccount != null)
        {
            var feeEntries = await _dbContext.LedgerEntries
                .Where(e => e.LedgerAccountId == feeAccount.Id)
                .ToListAsync(cancellationToken);

            var credits = feeEntries.Where(e => e.Direction == LedgerEntryDirection.Credit).Sum(e => e.Amount);
            var debits = feeEntries.Where(e => e.Direction == LedgerEntryDirection.Debit).Sum(e => e.Amount);
            availableBalance = credits - debits;
        }

        // 2. Aggregate Pending Settlements (outbound bank transfers + pending funding)
        var pendingBankTransfers = await _dbContext.BankTransfers
            .Where(b => b.Status == BankTransferStatus.Pending && b.Currency == targetCurrency)
            .ToListAsync(cancellationToken);
        var pendingTransferAmount = pendingBankTransfers.Sum(b => b.Amount);

        var pendingFunding = await _dbContext.FundingTransactions
            .Where(f => (f.Status == FundingTransactionStatus.Pending || f.Status == FundingTransactionStatus.Processing)
                        && f.Currency == targetCurrency)
            .ToListAsync(cancellationToken);
        var pendingFundingAmount = pendingFunding.Sum(f => f.Amount);

        var pendingSettlement = pendingTransferAmount + pendingFundingAmount;
        var ledgerBalance = availableBalance + pendingSettlement;

        // 3. Resolve latest reconciliation timestamp
        var lastReconciliationRecord = await _dbContext.ReconciliationRecords
            .OrderByDescending(r => r.ResolvedAtUtc ?? r.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var lastReconciliationAt = lastReconciliationRecord?.ResolvedAtUtc ?? lastReconciliationRecord?.UpdatedAtUtc;

        // 4. Currency Symbol
        var symbol = targetCurrency switch
        {
            Currency.NGN => "₦",
            Currency.INTERNATIONAL_NGN => "₦",
            Currency.USD => "$",
            Currency.USDT => "₮",
            Currency.EUR => "€",
            Currency.GHS => "₵",
            Currency.INR => "₹",
            _ => targetCurrency.ToString()
        };

        return new PlatformTreasurySummaryDto(
            Currency: targetCurrency.ToString(),
            Symbol: symbol,
            AvailableBalance: decimal.Round(availableBalance, 2, MidpointRounding.AwayFromZero),
            LedgerBalance: decimal.Round(ledgerBalance, 2, MidpointRounding.AwayFromZero),
            PendingSettlement: decimal.Round(pendingSettlement, 2, MidpointRounding.AwayFromZero),
            LastReconciliationAt: lastReconciliationAt);
    }
}
