using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Domain.Finance.Models;

/// <summary>
/// Domain model encapsulating calculated fee amounts and settlement allocations according to the active fee policy.
/// </summary>
public sealed record FeeBreakdown(
    decimal Amount,
    decimal Fee,
    FeeBearer FeeBearer,
    decimal TotalCustomerCharge,
    decimal NetBeneficiaryCredit,
    decimal PlatformFeeCost);
