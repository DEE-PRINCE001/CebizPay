#pragma warning disable CA1707
namespace CebizPay.Domain.Finance.Enums;

/// <summary>
/// Supported currencies in CebizPay.
/// Transactional V1 currencies: NGN, INTERNATIONAL_NGN, USDT.
/// Reporting currencies: USD, GHS, EUR, INR.
/// </summary>
public enum Currency
{
    /// <summary>Nigerian Naira.</summary>
    NGN = 1,
    /// <summary>International Nigerian Naira.</summary>
    INTERNATIONAL_NGN = 2,
    /// <summary>Tether USD Stablecoin.</summary>
    USDT = 3,
    /// <summary>US Dollar (Reporting/Display).</summary>
    USD = 4,
    /// <summary>Ghanaian Cedi (Reporting/Display).</summary>
    GHS = 5,
    /// <summary>Euro (Reporting/Display).</summary>
    EUR = 6,
    /// <summary>Indian Rupee (Reporting/Display).</summary>
    INR = 7
}

/// <summary>
/// Helper extensions for Currency enum enforcing transactional vs reporting currency boundaries.
/// </summary>
public static class CurrencyExtensions
{
    /// <summary>
    /// Returns true if currency is supported for transactional operations in V1.
    /// </summary>
    public static bool IsTransactionalV1(this Currency currency)
    {
        return currency is Currency.NGN or Currency.INTERNATIONAL_NGN or Currency.USDT;
    }

    /// <summary>
    /// Centralized enforcement rule: throws an ArgumentException if the currency is not transactionally supported in V1.
    /// </summary>
    public static void EnsureTransactionalV1(this Currency currency)
    {
        if (!currency.IsTransactionalV1())
        {
            throw new ArgumentException($"Currency '{currency}' is a reporting-only currency and cannot be used for transactional wallets or financial operations in V1.", nameof(currency));
        }
    }
}
