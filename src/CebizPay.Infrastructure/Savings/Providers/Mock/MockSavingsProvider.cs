#pragma warning disable CA1848, CS1591
using System.Collections.Concurrent;
using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Savings;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Savings.Enums;

namespace CebizPay.Infrastructure.Savings.Providers.Mock;

/// <summary>
/// In-memory simulator implementing ISavingsProvider for testing and offline development.
/// </summary>
public sealed class MockSavingsProvider : ISavingsProvider
{
    private readonly ConcurrentDictionary<string, ExternalSavingsPlanRecord> _plans = new();
    private readonly ConcurrentDictionary<string, string> _customers = new();

    /// <inheritdoc/>
    public string ProviderName => "Mock";

    /// <inheritdoc/>
    public Task<ExternalCustomerResult> EnsureCustomerAsync(ExternalCustomerRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var customerId = _customers.GetOrAdd(request.UserId, _ => $"mock_cst_{Guid.NewGuid():N}"[..24]);
        return Task.FromResult(new ExternalCustomerResult(
            customerId,
            $"mock_wal_{customerId[9..]}",
            true,
            "ACTIVE"));
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ExternalSavingsProductRate>> GetProductRatesAsync(Currency currency, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ExternalSavingsProductRate> rates =
        [
            new ExternalSavingsProductRate("MOCK-LOCK", SavingsPlanType.FixedLock, 0.15m, 30, 730, 0.025m, "Mock Fixed Lock"),
            new ExternalSavingsProductRate("MOCK-GOAL", SavingsPlanType.GoalBased, 0.12m, 30, 730, 0.0m, "Mock Goal Savings")
        ];

        return Task.FromResult(rates);
    }

    /// <inheritdoc/>
    public Task<ExternalSavingsPlanResult> CreatePlanAsync(ExternalCreatePlanRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var planId = $"mock_plan_{Guid.NewGuid():N}"[..24];
        var rate = request.PlanType == SavingsPlanType.FixedLock ? 0.15m : 0.12m;

        var record = new ExternalSavingsPlanRecord(
            planId,
            request.ExternalCustomerId,
            request.PlanName,
            request.PlanType,
            request.Currency,
            0m,
            0m,
            rate,
            request.DurationDays,
            request.MaturityDateUtc,
            "active");

        _plans[planId] = record;

        return Task.FromResult(new ExternalSavingsPlanResult(
            planId,
            $"019{Random.Shared.Next(1000000, 9999999)}",
            "active",
            rate,
            request.MaturityDateUtc));
    }

    /// <inheritdoc/>
    public Task<ExternalFundingResult> FundPlanAsync(ExternalFundingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_plans.TryGetValue(request.ExternalPlanId, out var existing))
        {
            var updated = existing with { PrincipalBalance = existing.PrincipalBalance + request.Amount };
            _plans[request.ExternalPlanId] = updated;
        }

        return Task.FromResult(new ExternalFundingResult(
            $"mock_tx_{Guid.NewGuid():N}"[..24],
            true,
            request.Amount,
            DateTime.UtcNow));
    }

    /// <inheritdoc/>
    public Task<ExternalSavingsPosition> GetPositionAsync(string externalPlanId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalPlanId))
            throw new ArgumentException("ExternalPlanId is required.", nameof(externalPlanId));

        if (!_plans.TryGetValue(externalPlanId, out var record))
        {
            return Task.FromResult(new ExternalSavingsPosition(
                externalPlanId,
                0m,
                0m,
                0m,
                false,
                "unknown",
                DateTime.UtcNow));
        }

        var isMatured = DateTime.UtcNow >= record.MaturityDateUtc;
        var dailyRate = record.AnnualRate > 0 ? (record.AnnualRate / 365m) : (0.15m / 365m);
        var currentAccrued = record.AccruedInterest > 0
            ? record.AccruedInterest
            : Math.Round(record.PrincipalBalance * dailyRate, 4, MidpointRounding.AwayFromZero);

        if (currentAccrued > record.AccruedInterest)
        {
            _plans[externalPlanId] = record with { AccruedInterest = currentAccrued };
        }

        return Task.FromResult(new ExternalSavingsPosition(
            record.PlanId,
            record.PrincipalBalance,
            currentAccrued,
            currentAccrued,
            isMatured,
            isMatured ? "matured" : record.Status,
            DateTime.UtcNow));
    }

    /// <inheritdoc/>
    public Task<ExternalLiquidationResult> LiquidatePlanAsync(ExternalLiquidationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        decimal penalty = 0m;
        decimal forfeited = 0m;

        if (_plans.TryGetValue(request.ExternalPlanId, out var record))
        {
            if (request.IsEarlyExit)
            {
                penalty = Math.Round(record.PrincipalBalance * 0.025m, 2, MidpointRounding.AwayFromZero);
                forfeited = record.AccruedInterest;
            }

            _plans[request.ExternalPlanId] = record with
            {
                PrincipalBalance = 0m,
                AccruedInterest = 0m,
                Status = "liquidated"
            };
        }

        var netPayout = request.Amount - penalty;

        return Task.FromResult(new ExternalLiquidationResult(
            $"mock_liq_{Guid.NewGuid():N}"[..24],
            request.Amount,
            penalty,
            forfeited,
            netPayout,
            true,
            DateTime.UtcNow));
    }

    /// <inheritdoc/>
    public Task<SavingsWebhookEvent?> ParseWebhookAsync(string rawPayload, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
            return Task.FromResult<SavingsWebhookEvent?>(null);

        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            var root = doc.RootElement;
            var eventType = root.TryGetProperty("eventType", out var evProp) ? evProp.GetString() ?? "unknown" : "unknown";
            var planId = root.TryGetProperty("externalPlanId", out var planProp) ? planProp.GetString() : null;

            return Task.FromResult<SavingsWebhookEvent?>(new SavingsWebhookEvent(
                eventType,
                ProviderName,
                planId,
                null,
                null,
                DateTime.UtcNow,
                Guid.NewGuid().ToString("N"),
                rawPayload));
        }
        catch
        {
            return Task.FromResult<SavingsWebhookEvent?>(null);
        }
    }

    private sealed record ExternalSavingsPlanRecord(
        string PlanId,
        string CustomerId,
        string PlanName,
        SavingsPlanType PlanType,
        Currency Currency,
        decimal PrincipalBalance,
        decimal AccruedInterest,
        decimal AnnualRate,
        int DurationDays,
        DateTime MaturityDateUtc,
        string Status);
}
