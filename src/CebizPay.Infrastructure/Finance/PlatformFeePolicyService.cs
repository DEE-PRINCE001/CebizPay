using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Finance.Events;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Finance;

/// <summary>
/// Infrastructure implementation of <see cref="IPlatformFeePolicyService"/> managing versioned,
/// operation-specific platform fee policy persistence and activation lifecycles.
/// </summary>
public sealed partial class PlatformFeePolicyService : IPlatformFeePolicyService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IOutboxService _outboxService;
    private readonly ILogger<PlatformFeePolicyService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformFeePolicyService"/> class.
    /// </summary>
    public PlatformFeePolicyService(
        ApplicationDbContext dbContext,
        IOutboxService outboxService,
        ILogger<PlatformFeePolicyService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<PlatformFeePolicy?> GetActivePolicyAsync(
        FeeOperationType operationType,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PlatformFeePolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.OperationType == operationType && p.IsEnabled, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PlatformFeePolicy>> GetAllPoliciesAsync(
        FeeOperationType? operationType = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.PlatformFeePolicies.AsNoTracking();

        if (operationType.HasValue)
        {
            query = query.Where(p => p.OperationType == operationType.Value);
        }

        return await query
            .OrderBy(p => p.OperationType)
            .ThenByDescending(p => p.Version)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<PlatformFeePolicy> CreateAndActivatePolicyAsync(
        FeeOperationType operationType,
        FeeCalculationMethod calculationMethod,
        FeeBearer feeBearer,
        decimal? fixedAmount,
        decimal? percentageRate,
        decimal? minimumFee,
        decimal? maximumFee,
        Currency currency,
        string createdByUserId,
        DateTime effectiveFromUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.ExecuteInTransactionAsync(async ct =>
            {
                // Determine next version number for this specific operation type
                var maxVersion = await _dbContext.PlatformFeePolicies
                    .Where(p => p.OperationType == operationType)
                    .MaxAsync(p => (int?)p.Version, ct)
                    .ConfigureAwait(false) ?? 0;

                var nextVersion = maxVersion + 1;

                // Deactivate any currently active policy for this operation type
                var currentlyActive = await _dbContext.PlatformFeePolicies
                    .Where(p => p.OperationType == operationType && p.IsEnabled)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                if (currentlyActive.Count > 0)
                {
                    foreach (var activePolicy in currentlyActive)
                    {
                        activePolicy.Deactivate();

                        _outboxService.Write(new PlatformFeePolicyDeactivatedDomainEvent(
                            PolicyId: activePolicy.Id,
                            OperationType: activePolicy.OperationType,
                            Version: activePolicy.Version,
                            OccurredOnUtc: DateTime.UtcNow));
                    }
                    await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                }

                // Create and persist the new version
                var newPolicy = PlatformFeePolicy.Create(
                    operationType: operationType,
                    calculationMethod: calculationMethod,
                    feeBearer: feeBearer,
                    fixedAmount: fixedAmount,
                    percentageRate: percentageRate,
                    minimumFee: minimumFee,
                    maximumFee: maximumFee,
                    currency: currency,
                    version: nextVersion,
                    createdByUserId: createdByUserId,
                    effectiveFromUtc: effectiveFromUtc);

                _dbContext.PlatformFeePolicies.Add(newPolicy);

                _outboxService.Write(new PlatformFeePolicyCreatedDomainEvent(
                    PolicyId: newPolicy.Id,
                    OperationType: newPolicy.OperationType,
                    Version: newPolicy.Version,
                    CalculationMethod: newPolicy.CalculationMethod,
                    FeeBearer: newPolicy.FeeBearer,
                    OccurredOnUtc: DateTime.UtcNow));

                await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

                LogPolicyCreated(_logger, newPolicy.OperationType, newPolicy.Version, newPolicy.CalculationMethod);
                return newPolicy;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogPolicyCreationFailure(_logger, operationType, ex);
            throw;
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Platform fee policy created for {OperationType} v{Version} with method {Method}")]
    private static partial void LogPolicyCreated(ILogger logger, FeeOperationType operationType, int version, FeeCalculationMethod method);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Failed to create platform fee policy for {OperationType}")]
    private static partial void LogPolicyCreationFailure(ILogger logger, FeeOperationType operationType, Exception exception);
}
