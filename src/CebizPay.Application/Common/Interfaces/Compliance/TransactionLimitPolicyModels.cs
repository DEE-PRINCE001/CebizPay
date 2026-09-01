#pragma warning disable CS1591
using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Application.Common.Interfaces.Compliance;

/// <summary>
/// Identifies which regulatory, product, provider, or account risk layer dictates a transaction limit.
/// </summary>
public enum LimitConstraintSource
{
    /// <summary>Non-overridable statutory ceiling established by Central Bank of Nigeria (CBN) regulations.</summary>
    RegulatoryCbnTierLimit,

    /// <summary>Configurable business policy established by CebizPay product management within regulatory bounds.</summary>
    CebizPayProductPolicy,

    /// <summary>External rail constraint imposed by upstream payment infrastructure provider (e.g. Monnify, Flutterwave).</summary>
    PaymentProviderRailConstraint,

    /// <summary>Account-specific risk restriction placed directly on an individual customer or organization.</summary>
    CustomerRiskRestriction
}

/// <summary>
/// Computed effective transaction limit detailing all contributing layers and the binding constraint.
/// </summary>
public sealed record EffectiveTransactionLimit(
    decimal EffectiveSingleCap,
    decimal EffectiveDailyCap,
    decimal? RegulatorySingleCap,
    decimal? ProductSingleCap,
    decimal? ProviderSingleCap,
    decimal? CustomerSingleCap,
    LimitConstraintSource BindingConstraintSource,
    string Explanation,
    string PolicyVersion);

/// <summary>
/// Immutable, versioned definition of transaction limit policies and thresholds.
/// </summary>
public sealed class TransactionLimitPolicy
{
    public const string DefaultVersion = "2026.1";

    public string PolicyId { get; init; } = "POL-NGN-DEFAULT";
    public string Version { get; init; } = DefaultVersion;
    public DateTime EffectiveFromUtc { get; init; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // =========================================================================
    // 1. NON-OVERRIDABLE REGULATORY STATUTORY CEILINGS (CBN Tiered KYC Guidelines)
    // Applies strictly to natural persons (individuals), never to legal persons.
    // =========================================================================
    public decimal CbnTier1SingleCeiling { get; init; } = 50_000m;
    public decimal CbnTier1DailyCeiling { get; init; } = 300_000m;
    public decimal CbnTier2SingleCeiling { get; init; } = 200_000m;
    public decimal CbnTier2DailyCeiling { get; init; } = 1_000_000m;
    public decimal CbnTier3SingleCeiling { get; init; } = decimal.MaxValue;
    public decimal CbnTier3DailyCeiling { get; init; } = decimal.MaxValue;

    // =========================================================================
    // 2. CONFIGURABLE CEBIZPAY PRODUCT LIMITS (Must remain <= Regulatory Ceilings)
    // =========================================================================
    public decimal ConfiguredIndividualTier1SingleCap { get; init; } = 50_000m;
    public decimal ConfiguredIndividualTier1DailyCap { get; init; } = 300_000m;
    public decimal ConfiguredIndividualTier2SingleCap { get; init; } = 200_000m;
    public decimal ConfiguredIndividualTier2DailyCap { get; init; } = 1_000_000m;
    public decimal ConfiguredIndividualTier3SingleCap { get; init; } = 10_000_000m;
    public decimal ConfiguredIndividualTier3DailyCap { get; init; } = 50_000_000m;

    public decimal ConfiguredCorporateSingleCap { get; init; } = 25_000_000m;
    public decimal ConfiguredCorporateDailyCap { get; init; } = 100_000_000m;

    public decimal ConfiguredPeerTransferSingleCap { get; init; } = 1_000_000m;
    public decimal ConfiguredVasPurchaseSingleCap { get; init; } = 100_000m;

    // =========================================================================
    // 3. EXTERNAL PAYMENT PROVIDER RAIL CONSTRAINTS
    // =========================================================================
    public decimal ProviderFlutterwaveCardFundingSingleCap { get; init; } = 2_000_000m;
    public decimal ProviderPaystackCardFundingSingleCap { get; init; } = 1_000_000m;
    public decimal ProviderMonnifyBankTransferSingleCap { get; init; } = 10_000_000m;
    public decimal ProviderDefaultSingleCap { get; init; } = 10_000_000m;

    // =========================================================================
    // 4. RISK PROFILE VOLUME & EDD TRIGGER THRESHOLDS
    // =========================================================================
    public decimal IndividualEddVolumeThreshold { get; init; } = 5_000_000m;
    public decimal CorporateEddVolumeThreshold { get; init; } = 20_000_000m;
    public decimal IndividualElevatedMonitoringThreshold { get; init; } = 1_000_000m;
    public decimal CorporateElevatedMonitoringThreshold { get; init; } = 5_000_000m;

    /// <summary>
    /// Computes the effective single and daily caps by evaluating Regulatory Ceilings,
    /// Product Policy, Provider Rails, and Customer Restrictions in order of precedence.
    /// </summary>
    public EffectiveTransactionLimit CalculateEffectiveLimit(
        RiskSubjectType subjectType,
        int? individualTier,
        ComplianceOperationType operationType,
        decimal? customerSingleCap = null,
        string? provider = null)
    {
        decimal? regulatorySingleCap = null;
        decimal? regulatoryDailyCap = null;

        // 1. Regulatory Limits (Individuals only)
        if (subjectType == RiskSubjectType.Individual)
        {
            var tier = individualTier ?? 1;
            regulatorySingleCap = tier switch
            {
                1 => CbnTier1SingleCeiling,
                2 => CbnTier2SingleCeiling,
                _ => CbnTier3SingleCeiling
            };

            regulatoryDailyCap = tier switch
            {
                1 => CbnTier1DailyCeiling,
                2 => CbnTier2DailyCeiling,
                _ => CbnTier3DailyCeiling
            };
        }

        // 2. Product Limits (Raw configured by CebizPay)
        decimal rawProductSingleCap;
        decimal rawProductDailyCap;

        if (subjectType == RiskSubjectType.Individual)
        {
            var tier = individualTier ?? 1;
            rawProductSingleCap = tier switch
            {
                1 => ConfiguredIndividualTier1SingleCap,
                2 => ConfiguredIndividualTier2SingleCap,
                _ => ConfiguredIndividualTier3SingleCap
            };

            rawProductDailyCap = tier switch
            {
                1 => ConfiguredIndividualTier1DailyCap,
                2 => ConfiguredIndividualTier2DailyCap,
                _ => ConfiguredIndividualTier3DailyCap
            };

            if (operationType == ComplianceOperationType.PeerTransfer)
            {
                rawProductSingleCap = Math.Min(rawProductSingleCap, ConfiguredPeerTransferSingleCap);
            }
            else if (operationType == ComplianceOperationType.VasPurchase)
            {
                rawProductSingleCap = Math.Min(rawProductSingleCap, ConfiguredVasPurchaseSingleCap);
            }
        }
        else
        {
            // Corporate / Legal Person: Tiered KYC does not apply
            rawProductSingleCap = ConfiguredCorporateSingleCap;
            rawProductDailyCap = ConfiguredCorporateDailyCap;
        }

        // 3. Provider Rail Limits
        decimal? providerSingleCap = null;
        if (!string.IsNullOrWhiteSpace(provider))
        {
            var p = provider.Trim().ToLowerInvariant();
            if (p.Contains("flutterwave") && operationType == ComplianceOperationType.CardFunding)
                providerSingleCap = ProviderFlutterwaveCardFundingSingleCap;
            else if (p.Contains("paystack") && operationType == ComplianceOperationType.CardFunding)
                providerSingleCap = ProviderPaystackCardFundingSingleCap;
            else if (p.Contains("monnify"))
                providerSingleCap = ProviderMonnifyBankTransferSingleCap;
            else
                providerSingleCap = ProviderDefaultSingleCap;
        }

        // 4. Customer-specific restriction
        decimal? customerCap = customerSingleCap;

        // 5. Determine lowest binding single cap across layers
        var effectiveSingle = rawProductSingleCap;
        var bindingSource = LimitConstraintSource.CebizPayProductPolicy;

        // Regulatory Ceiling has supreme non-overridable precedence for individuals
        if (regulatorySingleCap.HasValue && regulatorySingleCap.Value <= effectiveSingle)
        {
            effectiveSingle = regulatorySingleCap.Value;
            if (rawProductSingleCap >= regulatorySingleCap.Value)
            {
                bindingSource = LimitConstraintSource.RegulatoryCbnTierLimit;
            }
        }

        if (providerSingleCap.HasValue && providerSingleCap.Value < effectiveSingle)
        {
            effectiveSingle = providerSingleCap.Value;
            bindingSource = LimitConstraintSource.PaymentProviderRailConstraint;
        }

        if (customerCap.HasValue && customerCap.Value < effectiveSingle)
        {
            effectiveSingle = customerCap.Value;
            bindingSource = LimitConstraintSource.CustomerRiskRestriction;
        }

        var effectiveDaily = rawProductDailyCap;
        if (regulatoryDailyCap.HasValue && regulatoryDailyCap.Value < effectiveDaily)
        {
            effectiveDaily = regulatoryDailyCap.Value;
        }

        var explanation = bindingSource switch
        {
            LimitConstraintSource.RegulatoryCbnTierLimit =>
                $"Statutory CBN Tier {individualTier ?? 1} non-overridable limit of ₦{effectiveSingle:N2}.",
            LimitConstraintSource.CebizPayProductPolicy =>
                $"CebizPay policy limit of ₦{effectiveSingle:N2} for {operationType}.",
            LimitConstraintSource.PaymentProviderRailConstraint =>
                $"Payment provider ({provider}) rail constraint limit of ₦{effectiveSingle:N2}.",
            LimitConstraintSource.CustomerRiskRestriction =>
                $"Custom account risk restriction single cap of ₦{effectiveSingle:N2}.",
            _ => $"Effective limit of ₦{effectiveSingle:N2} applied."
        };

        return new EffectiveTransactionLimit(
            EffectiveSingleCap: effectiveSingle,
            EffectiveDailyCap: effectiveDaily,
            RegulatorySingleCap: regulatorySingleCap,
            ProductSingleCap: rawProductSingleCap,
            ProviderSingleCap: providerSingleCap,
            CustomerSingleCap: customerCap,
            BindingConstraintSource: bindingSource,
            Explanation: explanation,
            PolicyVersion: Version);
    }
}
