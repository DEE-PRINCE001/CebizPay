using CebizPay.Application.Common.Interfaces.Analytics;
using CebizPay.Application.UseCases.Admin.Analytics;
using CebizPay.Domain.Finance.Enums;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Infrastructure.Persistence.Queries;

/// <summary>
/// Infrastructure service for executing optimized database-side analytics queries for platform fee revenue and transaction volumes.
/// </summary>
public sealed class PlatformAnalyticsQueryService : IPlatformAnalyticsQueryService
{
    private static readonly string[] MonthNames = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
    private readonly ApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="PlatformAnalyticsQueryService"/>.
    /// </summary>
    public PlatformAnalyticsQueryService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc/>
    public async Task<PlatformRevenueAnalyticsDto> GetYearlyRevenueAnalyticsAsync(
        int year,
        Currency currency,
        CancellationToken cancellationToken = default)
    {
        var yearStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearEnd = yearStart.AddYears(1);

        // 1. Query fee revenue entries for the target year
        var feeEntries = await (
            from entry in _dbContext.LedgerEntries
            join account in _dbContext.LedgerAccounts on entry.LedgerAccountId equals account.Id
            join txn in _dbContext.LedgerTransactions on entry.LedgerTransactionId equals txn.Id
            where account.AccountType == LedgerAccountType.FeeRevenue
               && account.Currency == currency
               && (txn.CompletedAtUtc ?? txn.CreatedAtUtc) >= yearStart
               && (txn.CompletedAtUtc ?? txn.CreatedAtUtc) < yearEnd
               && txn.Status == LedgerTransactionStatus.Completed
            select new
            {
                entry.Amount,
                entry.Direction,
                Month = (txn.CompletedAtUtc ?? txn.CreatedAtUtc).Month
            }
        ).ToListAsync(cancellationToken);

        var monthlyRevenueMap = new Dictionary<int, decimal>();
        for (int m = 1; m <= 12; m++)
        {
            monthlyRevenueMap[m] = 0m;
        }

        foreach (var entry in feeEntries)
        {
            var delta = entry.Direction == LedgerEntryDirection.Credit ? entry.Amount : -entry.Amount;
            monthlyRevenueMap[entry.Month] += delta;
        }

        // 2. Query transaction volume entries for the target year (primary Sequence 1 entries of non-reversal completed txns)
        var volumeEntries = await (
            from entry in _dbContext.LedgerEntries
            join txn in _dbContext.LedgerTransactions on entry.LedgerTransactionId equals txn.Id
            where entry.Sequence == 1
               && entry.Currency == currency
               && (txn.CompletedAtUtc ?? txn.CreatedAtUtc) >= yearStart
               && (txn.CompletedAtUtc ?? txn.CreatedAtUtc) < yearEnd
               && txn.Status == LedgerTransactionStatus.Completed
               && txn.TransactionType != LedgerTransactionType.Reversal
            select new
            {
                entry.Amount,
                Month = (txn.CompletedAtUtc ?? txn.CreatedAtUtc).Month
            }
        ).ToListAsync(cancellationToken);

        var monthlyVolumeMap = new Dictionary<int, decimal>();
        for (int m = 1; m <= 12; m++)
        {
            monthlyVolumeMap[m] = 0m;
        }

        foreach (var entry in volumeEntries)
        {
            monthlyVolumeMap[entry.Month] += entry.Amount;
        }

        // 3. Compute totals
        var totalRevenue = monthlyRevenueMap.Values.Sum();
        var totalVolume = monthlyVolumeMap.Values.Sum();

        // 4. Compute Month-over-Month (MoM) revenue growth rate
        var now = DateTime.UtcNow;
        int activeMonth = now.Year == year ? now.Month : 12;

        decimal currentMonthRev = monthlyRevenueMap[activeMonth];
        decimal prevMonthRev = 0m;

        if (activeMonth > 1)
        {
            prevMonthRev = monthlyRevenueMap[activeMonth - 1];
        }
        else
        {
            // January: look up previous year's December fee revenue
            var prevDecStart = new DateTime(year - 1, 12, 1, 0, 0, 0, DateTimeKind.Utc);
            var prevDecEnd = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var prevDecEntries = await (
                from entry in _dbContext.LedgerEntries
                join account in _dbContext.LedgerAccounts on entry.LedgerAccountId equals account.Id
                join txn in _dbContext.LedgerTransactions on entry.LedgerTransactionId equals txn.Id
                where account.AccountType == LedgerAccountType.FeeRevenue
                   && account.Currency == currency
                   && (txn.CompletedAtUtc ?? txn.CreatedAtUtc) >= prevDecStart
                   && (txn.CompletedAtUtc ?? txn.CreatedAtUtc) < prevDecEnd
                   && txn.Status == LedgerTransactionStatus.Completed
                select new
                {
                    entry.Amount,
                    entry.Direction
                }
            ).ToListAsync(cancellationToken);

            prevMonthRev = prevDecEntries.Sum(e => e.Direction == LedgerEntryDirection.Credit ? e.Amount : -e.Amount);
        }

        double momGrowthRate = 0.0;
        if (prevMonthRev > 0)
        {
            momGrowthRate = (double)Math.Round(((currentMonthRev - prevMonthRev) / prevMonthRev) * 100m, 1, MidpointRounding.AwayFromZero);
        }

        // 5. Build 12-month data series
        var monthlyData = new List<MonthlyDataPointDto>(12);
        for (int m = 1; m <= 12; m++)
        {
            monthlyData.Add(new MonthlyDataPointDto(
                Name: MonthNames[m - 1],
                Revenue: decimal.Round(monthlyRevenueMap[m], 2, MidpointRounding.AwayFromZero),
                Volume: decimal.Round(monthlyVolumeMap[m], 2, MidpointRounding.AwayFromZero)));
        }

        return new PlatformRevenueAnalyticsDto(
            Year: year,
            TotalRevenue: decimal.Round(totalRevenue, 2, MidpointRounding.AwayFromZero),
            TotalTransactionVolume: decimal.Round(totalVolume, 2, MidpointRounding.AwayFromZero),
            MomGrowthRate: momGrowthRate,
            MonthlyData: monthlyData);
    }
}
