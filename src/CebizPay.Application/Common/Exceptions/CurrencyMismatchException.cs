namespace CebizPay.Application.Common.Exceptions;

/// <summary>
/// Thrown when the source and recipient wallet currencies do not match.
/// Peer transfers require same-currency wallets in V1.
/// Maps to HTTP 422 Unprocessable Entity with code WALLET_CURRENCY_MISMATCH.
/// </summary>
public sealed class CurrencyMismatchException : Exception
{
    /// <summary>Stable machine-readable error code.</summary>
    public string Code { get; } = "WALLET_CURRENCY_MISMATCH";

    /// <summary>
    /// Initializes a new instance of <see cref="CurrencyMismatchException"/>.
    /// </summary>
    public CurrencyMismatchException(string sourceCurrency, string recipientCurrency)
        : base($"Currency mismatch: source wallet currency ({sourceCurrency}) differs from recipient wallet currency ({recipientCurrency}). Cross-currency peer transfers are not supported in V1.")
    {
    }
}
