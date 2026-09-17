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

namespace CebizPay.Infrastructure.Savings.Providers.Anchor;

/// <summary>
/// Provider adapter integrating Anchor BaaS subledgers and book transfers with CebizPay savings architecture.
/// </summary>
public sealed class AnchorSavingsProvider : ISavingsProvider
{
    private readonly IAnchorClient _client;
    private readonly AnchorOptions _options;
    private readonly ILogger<AnchorSavingsProvider> _logger;

    /// <inheritdoc/>
    public string ProviderName => "Anchor";

    /// <summary>
    /// Initializes a new instance of <see cref="AnchorSavingsProvider"/>.
    /// </summary>
    public AnchorSavingsProvider(
        IAnchorClient client,
        IOptions<AnchorOptions> options,
        ILogger<AnchorSavingsProvider> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<ExternalCustomerResult> EnsureCustomerAsync(ExternalCustomerRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var customerEnvelope = new AnchorResourceEnvelope<AnchorResource<AnchorIndividualCustomerAttributes>>
        {
            Data = new AnchorResource<AnchorIndividualCustomerAttributes>
            {
                Type = "IndividualCustomer",
                Attributes = new AnchorIndividualCustomerAttributes
                {
                    FullName = new AnchorFullName
                    {
                        FirstName = request.FirstName,
                        LastName = request.LastName
                    },
                    Email = request.Email,
                    PhoneNumber = request.PhoneNumber,
                    Bvn = request.Bvn
                }
            }
        };

        var result = await _client.CreateCustomerAsync(customerEnvelope, cancellationToken);
        if (result == null || string.IsNullOrWhiteSpace(result.Id))
        {
            throw new InvalidOperationException($"Failed to provision customer profile at Anchor for user '{request.UserId}'.");
        }

        return new ExternalCustomerResult(
            result.Id,
            null,
            string.Equals(result.Attributes?.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase),
            result.Attributes?.Status);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ExternalSavingsProductRate>> GetProductRatesAsync(Currency currency, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ExternalSavingsProductRate> rates =
        [
            new ExternalSavingsProductRate("ANC-LOCK", SavingsPlanType.FixedLock, 0.14m, 30, 730, 0.025m, "Anchor Fixed Lock Subledger"),
            new ExternalSavingsProductRate("ANC-GOAL", SavingsPlanType.GoalBased, 0.11m, 30, 730, 0.0m, "Anchor Goal Subledger")
        ];

        return Task.FromResult(rates);
    }

    /// <inheritdoc/>
    public async Task<ExternalSavingsPlanResult> CreatePlanAsync(ExternalCreatePlanRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(_options.ParentFboAccountId))
        {
            throw new InvalidOperationException("ParentFboAccountId is not configured for Anchor provider.");
        }

        var subAccountEnvelope = new AnchorResourceEnvelope<AnchorResource<AnchorSubAccountAttributes>>
        {
            Data = new AnchorResource<AnchorSubAccountAttributes>
            {
                Type = "SubAccount",
                Attributes = new AnchorSubAccountAttributes
                {
                    CreateVirtualNuban = false,
                    AccountType = request.PlanType == SavingsPlanType.FixedLock ? "FIXED_SAVINGS" : "GOAL_SAVINGS"
                },
                Relationships = new Dictionary<string, AnchorRelationship>
                {
                    ["customer"] = new AnchorRelationship
                    {
                        Data = new AnchorResourceRef { Id = request.ExternalCustomerId, Type = "IndividualCustomer" }
                    },
                    ["parentAccount"] = new AnchorRelationship
                    {
                        Data = new AnchorResourceRef { Id = _options.ParentFboAccountId, Type = "DepositAccount" }
                    }
                }
            }
        };

        var result = await _client.CreateSubAccountAsync(subAccountEnvelope, cancellationToken);
        if (result == null || string.IsNullOrWhiteSpace(result.Id))
        {
            throw new InvalidOperationException($"Failed to create Anchor subledger for plan '{request.PlanName}'.");
        }

        var rate = request.PlanType == SavingsPlanType.FixedLock ? 0.14m : 0.11m;

        return new ExternalSavingsPlanResult(
            result.Id,
            null,
            result.Attributes?.Status ?? "ACTIVE",
            rate,
            request.MaturityDateUtc);
    }

    /// <inheritdoc/>
    public async Task<ExternalFundingResult> FundPlanAsync(ExternalFundingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(_options.ParentFboAccountId))
        {
            throw new InvalidOperationException("ParentFboAccountId is not configured for Anchor provider.");
        }

        var transferEnvelope = new AnchorResourceEnvelope<AnchorResource<AnchorTransferAttributes>>
        {
            Data = new AnchorResource<AnchorTransferAttributes>
            {
                Type = "Transfer",
                Attributes = new AnchorTransferAttributes
                {
                    TransferType = "BookTransfer",
                    Amount = request.Amount,
                    Currency = request.Currency.ToString(),
                    Reference = request.TransactionReference
                },
                Relationships = new Dictionary<string, AnchorRelationship>
                {
                    ["sourceAccount"] = new AnchorRelationship
                    {
                        Data = new AnchorResourceRef { Id = _options.ParentFboAccountId, Type = "DepositAccount" }
                    },
                    ["destinationAccount"] = new AnchorRelationship
                    {
                        Data = new AnchorResourceRef { Id = request.ExternalPlanId, Type = "SubAccount" }
                    }
                }
            }
        };

        var result = await _client.CreateTransferAsync(transferEnvelope, cancellationToken);
        if (result == null || string.IsNullOrWhiteSpace(result.Id))
        {
            throw new InvalidOperationException($"Failed to execute funding book transfer to Anchor sub-account '{request.ExternalPlanId}'.");
        }

        var isCompleted = string.Equals(result.Attributes?.Status, "SUCCESSFUL", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(result.Attributes?.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase);

        return new ExternalFundingResult(
            result.Id,
            isCompleted,
            request.Amount,
            DateTime.UtcNow);
    }

    /// <inheritdoc/>
    public async Task<ExternalSavingsPosition> GetPositionAsync(string externalPlanId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalPlanId))
            throw new ArgumentException("ExternalPlanId is required.", nameof(externalPlanId));

        var subAccount = await _client.GetSubAccountAsync(externalPlanId, cancellationToken);
        if (subAccount == null)
        {
            throw new InvalidOperationException($"Failed to query Anchor sub-account '{externalPlanId}'.");
        }

        var balance = subAccount.Attributes?.Balance?.AvailableBalance ?? 0m;

        return new ExternalSavingsPosition(
            subAccount.Id ?? externalPlanId,
            balance,
            0m,
            0m,
            false,
            subAccount.Attributes?.Status ?? "ACTIVE",
            DateTime.UtcNow);
    }

    /// <inheritdoc/>
    public async Task<ExternalLiquidationResult> LiquidatePlanAsync(ExternalLiquidationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(_options.ParentFboAccountId))
        {
            throw new InvalidOperationException("ParentFboAccountId is not configured for Anchor provider.");
        }

        var penaltyRate = request.IsEarlyExit ? 0.025m : 0.0m;
        var penalty = Math.Round(request.Amount * penaltyRate, 2, MidpointRounding.AwayFromZero);
        var netPayout = request.Amount - penalty;

        var transferEnvelope = new AnchorResourceEnvelope<AnchorResource<AnchorTransferAttributes>>
        {
            Data = new AnchorResource<AnchorTransferAttributes>
            {
                Type = "Transfer",
                Attributes = new AnchorTransferAttributes
                {
                    TransferType = "BookTransfer",
                    Amount = netPayout,
                    Currency = "NGN",
                    Reference = request.TransactionReference
                },
                Relationships = new Dictionary<string, AnchorRelationship>
                {
                    ["sourceAccount"] = new AnchorRelationship
                    {
                        Data = new AnchorResourceRef { Id = request.ExternalPlanId, Type = "SubAccount" }
                    },
                    ["destinationAccount"] = new AnchorRelationship
                    {
                        Data = new AnchorResourceRef { Id = _options.ParentFboAccountId, Type = "DepositAccount" }
                    }
                }
            }
        };

        var result = await _client.CreateTransferAsync(transferEnvelope, cancellationToken);
        if (result == null || string.IsNullOrWhiteSpace(result.Id))
        {
            throw new InvalidOperationException($"Failed to execute liquidation book transfer for Anchor subledger '{request.ExternalPlanId}'.");
        }

        var isCompleted = string.Equals(result.Attributes?.Status, "SUCCESSFUL", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(result.Attributes?.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase);

        return new ExternalLiquidationResult(
            result.Id,
            request.Amount,
            penalty,
            0m,
            netPayout,
            isCompleted,
            DateTime.UtcNow);
    }

    /// <inheritdoc/>
    public Task<SavingsWebhookEvent?> ParseWebhookAsync(string rawPayload, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
            return Task.FromResult<SavingsWebhookEvent?>(null);

        if (!string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            if (!headers.TryGetValue("x-anchor-signature", out var signature) &&
                !headers.TryGetValue("X-Anchor-Signature", out signature))
            {
                _logger.LogWarning("Anchor webhook received without signature header.");
                return Task.FromResult<SavingsWebhookEvent?>(null);
            }

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.WebhookSecret));
            var computedHash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawPayload))).ToLowerInvariant();
            if (!string.Equals(computedHash, signature.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Invalid Anchor webhook signature.");
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
            decimal? amount = null;

            if (root.TryGetProperty("data", out var dataProp))
            {
                if (dataProp.TryGetProperty("id", out var id)) planId = id.GetString();
                if (dataProp.TryGetProperty("attributes", out var attrProp) &&
                    attrProp.TryGetProperty("amount", out var amtProp) &&
                    amtProp.TryGetDecimal(out var parsedAmt))
                {
                    amount = parsedAmt;
                }
            }

            return Task.FromResult<SavingsWebhookEvent?>(new SavingsWebhookEvent(
                eventType,
                ProviderName,
                planId,
                null,
                amount,
                DateTime.UtcNow,
                eventId,
                rawPayload));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Anchor webhook payload.");
            return Task.FromResult<SavingsWebhookEvent?>(null);
        }
    }
}
