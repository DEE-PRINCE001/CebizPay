#pragma warning disable CA1848, CS1591
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Savings;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Savings.Enums;
using CebizPay.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CebizPay.Infrastructure.Savings.Providers.Cowrywise;

/// <summary>
/// Provider adapter integrating Cowrywise Embed API with CebizPay savings architecture.
/// </summary>
public sealed class CowrywiseSavingsProvider : ISavingsProvider
{
    private readonly ICowrywiseClient _client;
    private readonly CowrywiseOptions _options;
    private readonly ILogger<CowrywiseSavingsProvider> _logger;

    /// <inheritdoc/>
    public string ProviderName => "Cowrywise";

    /// <summary>
    /// Initializes a new instance of <see cref="CowrywiseSavingsProvider"/>.
    /// </summary>
    public CowrywiseSavingsProvider(
        ICowrywiseClient client,
        IOptions<CowrywiseOptions> options,
        ILogger<CowrywiseSavingsProvider> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<ExternalCustomerResult> EnsureCustomerAsync(ExternalCustomerRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var accountReq = new CowrywiseCreateAccountRequest
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            Identity = !string.IsNullOrWhiteSpace(request.Bvn)
                ? new CowrywiseIdentity { Type = "bvn", Value = request.Bvn }
                : null
        };

        var result = await _client.CreateAccountAsync(accountReq, cancellationToken);
        if (result == null || string.IsNullOrWhiteSpace(result.AccountId))
        {
            throw new InvalidOperationException($"Failed to provision customer profile at Cowrywise for user '{request.UserId}'.");
        }

        return new ExternalCustomerResult(
            result.AccountId,
            result.WalletId,
            result.IsVerified,
            result.Status);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ExternalSavingsProductRate>> GetProductRatesAsync(Currency currency, CancellationToken cancellationToken = default)
    {
        var rates = await _client.GetRatesAsync(cancellationToken);
        if (rates == null || rates.Count == 0)
        {
            return
            [
                new ExternalSavingsProductRate("CW-LOCK", SavingsPlanType.FixedLock, 0.15m, 30, 730, 0.025m, "Cowrywise Fixed Lock"),
                new ExternalSavingsProductRate("CW-GOAL", SavingsPlanType.GoalBased, 0.12m, 30, 730, 0.0m, "Cowrywise Goal Savings")
            ];
        }

        return rates.Select(r => new ExternalSavingsProductRate(
            r.ProductCode,
            r.ProductCode.Contains("LOCK", StringComparison.OrdinalIgnoreCase) ? SavingsPlanType.FixedLock : SavingsPlanType.GoalBased,
            r.Rate > 1 ? r.Rate / 100m : r.Rate,
            r.MinDays,
            r.MaxDays,
            r.PenaltyRate > 1 ? r.PenaltyRate / 100m : r.PenaltyRate,
            r.Name)).ToList();
    }

    /// <inheritdoc/>
    public async Task<ExternalSavingsPlanResult> CreatePlanAsync(ExternalCreatePlanRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var createReq = new CowrywiseCreateSavingsRequest
        {
            AccountId = request.ExternalCustomerId,
            Name = request.PlanName,
            Currency = request.Currency.ToString(),
            DepositType = request.PlanType == SavingsPlanType.FixedLock ? "fixed" : "goal",
            Duration = request.DurationDays,
            InterestEnabled = true,
            TargetAmount = request.TargetAmount,
            IdempotencyKey = request.IdempotencyKey
        };

        var plan = await _client.CreateSavingsAsync(createReq, cancellationToken);
        if (plan == null || string.IsNullOrWhiteSpace(plan.Id))
        {
            throw new InvalidOperationException($"Failed to create savings plan '{request.PlanName}' at Cowrywise.");
        }

        return new ExternalSavingsPlanResult(
            plan.Id,
            null,
            plan.Status,
            plan.Rate > 1 ? plan.Rate / 100m : plan.Rate,
            plan.MaturityDate);
    }

    /// <inheritdoc/>
    public async Task<ExternalFundingResult> FundPlanAsync(ExternalFundingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fundReq = new CowrywiseFundSavingsRequest
        {
            Amount = request.Amount,
            Currency = request.Currency.ToString(),
            TransactionReference = request.TransactionReference
        };

        var result = await _client.FundSavingsAsync(request.ExternalPlanId, fundReq, cancellationToken);
        if (result == null || string.IsNullOrWhiteSpace(result.TransactionId))
        {
            throw new InvalidOperationException($"Failed to deposit {request.Amount} into Cowrywise plan '{request.ExternalPlanId}'.");
        }

        return new ExternalFundingResult(
            result.TransactionId,
            string.Equals(result.Status, "completed", StringComparison.OrdinalIgnoreCase),
            result.Amount > 0 ? result.Amount : request.Amount,
            DateTime.UtcNow);
    }

    /// <inheritdoc/>
    public async Task<ExternalSavingsPosition> GetPositionAsync(string externalPlanId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalPlanId))
            throw new ArgumentException("ExternalPlanId is required.", nameof(externalPlanId));

        var position = await _client.GetPositionAsync(externalPlanId, cancellationToken);
        if (position == null)
        {
            throw new InvalidOperationException($"Failed to retrieve position for Cowrywise plan '{externalPlanId}'.");
        }

        return new ExternalSavingsPosition(
            position.Id,
            position.PrincipalBalance,
            position.AccruedInterest,
            position.TotalYield,
            position.IsMatured,
            position.Status,
            DateTime.UtcNow);
    }

    /// <inheritdoc/>
    public async Task<ExternalLiquidationResult> LiquidatePlanAsync(ExternalLiquidationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var liqReq = new CowrywiseLiquidationRequest
        {
            AccountId = request.ExternalCustomerId,
            Amount = request.Amount,
            IsEarly = request.IsEarlyExit,
            TransactionReference = request.TransactionReference
        };

        var result = await _client.LiquidateSavingsAsync(request.ExternalPlanId, liqReq, cancellationToken);
        if (result == null || string.IsNullOrWhiteSpace(result.TransactionId))
        {
            throw new InvalidOperationException($"Failed to liquidate Cowrywise plan '{request.ExternalPlanId}'.");
        }

        return new ExternalLiquidationResult(
            result.TransactionId,
            result.GrossAmount,
            result.PenaltyAmount,
            result.ForfeitedInterest,
            result.NetAmount,
            string.Equals(result.Status, "completed", StringComparison.OrdinalIgnoreCase),
            DateTime.UtcNow);
    }

    /// <inheritdoc/>
    public Task<SavingsWebhookEvent?> ParseWebhookAsync(string rawPayload, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
            return Task.FromResult<SavingsWebhookEvent?>(null);

        // Verify webhook signature if secret configured
        if (!string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            if (!headers.TryGetValue("x-cowrywise-signature", out var signature) &&
                !headers.TryGetValue("X-Cowrywise-Signature", out signature))
            {
                _logger.LogWarning("Cowrywise webhook received without signature header.");
                return Task.FromResult<SavingsWebhookEvent?>(null);
            }

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.WebhookSecret));
            var computedHash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawPayload))).ToLowerInvariant();
            if (!string.Equals(computedHash, signature.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Invalid Cowrywise webhook signature.");
                return Task.FromResult<SavingsWebhookEvent?>(null);
            }
        }

        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            var root = doc.RootElement;
            var eventType = root.TryGetProperty("event", out var evProp) ? evProp.GetString() ?? "unknown" : "unknown";
            var eventId = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N");

            string? planId = null;
            string? customerId = null;
            decimal? amount = null;

            if (root.TryGetProperty("data", out var dataProp))
            {
                if (dataProp.TryGetProperty("savings_id", out var sId)) planId = sId.GetString();
                if (dataProp.TryGetProperty("account_id", out var aId)) customerId = aId.GetString();
                if (dataProp.TryGetProperty("amount", out var amtProp) && amtProp.TryGetDecimal(out var parsedAmt)) amount = parsedAmt;
            }

            return Task.FromResult<SavingsWebhookEvent?>(new SavingsWebhookEvent(
                eventType,
                ProviderName,
                planId,
                customerId,
                amount,
                DateTime.UtcNow,
                eventId,
                rawPayload));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Cowrywise webhook payload.");
            return Task.FromResult<SavingsWebhookEvent?>(null);
        }
    }
}
