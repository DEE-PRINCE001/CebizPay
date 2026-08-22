using CebizPay.Domain.Loans.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CebizPay.Workers;

/// <summary>
/// Background worker processing scheduled loan obligations, detecting overdue repayment installments,
/// and transitioning delinquent loan contracts without blocking HTTP operations.
/// </summary>
public sealed partial class LoanRepaymentWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LoanRepaymentWorker> _logger;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Initializes a new instance of the <see cref="LoanRepaymentWorker"/> class.
    /// </summary>
    public LoanRepaymentWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<LoanRepaymentWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogWorkerStarted(_logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOverdueInstallmentsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogWorkerLoopError(_logger, ex);
            }

            try
            {
                await Task.Delay(PollingInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        LogWorkerStopped(_logger);
    }

    private async Task ProcessOverdueInstallmentsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTime.UtcNow;

        // Find active or overdue contracts with past-due pending installments
        var overdueContracts = await dbContext.LoanContracts
            .Include(c => c.RepaymentSchedule)
            .Where(c => (c.Status == LoanContractStatus.Active || c.Status == LoanContractStatus.Overdue) &&
                        c.RepaymentSchedule.Any(i => i.DueDate < now && (i.Status == LoanRepaymentStatus.Pending || i.Status == LoanRepaymentStatus.Due)))
            .Take(50)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (overdueContracts.Count == 0)
        {
            return;
        }

        foreach (var contract in overdueContracts)
        {
            contract.CheckOverdue(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        LogContractsProcessed(_logger, overdueContracts.Count);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "LoanRepaymentWorker started.")]
    private static partial void LogWorkerStarted(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "LoanRepaymentWorker stopped.")]
    private static partial void LogWorkerStopped(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Unhandled error in LoanRepaymentWorker execution cycle.")]
    private static partial void LogWorkerLoopError(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Processed {Count} loan contracts for overdue repayment evaluations.")]
    private static partial void LogContractsProcessed(ILogger logger, int count);
}
