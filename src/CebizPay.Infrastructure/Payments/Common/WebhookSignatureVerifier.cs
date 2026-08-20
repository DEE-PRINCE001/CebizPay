using System.Security.Cryptography;
using System.Text;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Infrastructure.Payments.Common;

/// <summary>
/// Infrastructure implementation of <see cref="IWebhookSignatureVerifier"/> providing constant-time
/// cryptographic signature and token validation for Flutterwave and Paystack webhooks.
/// </summary>
public sealed class WebhookSignatureVerifier : IWebhookSignatureVerifier
{
    /// <inheritdoc/>
    public bool VerifySignature(
        PaymentProvider provider,
        string rawPayload,
        IReadOnlyDictionary<string, string> headers,
        string secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || headers == null)
            return false;

        return provider switch
        {
            PaymentProvider.Flutterwave => VerifyFlutterwave(headers, secret),
            PaymentProvider.Paystack => VerifyPaystack(rawPayload, headers, secret),
            _ => false
        };
    }

    private static bool VerifyFlutterwave(IReadOnlyDictionary<string, string> headers, string secret)
    {
        // Flutterwave sends secret hash in 'verif-hash' header
        var headerValue = GetHeaderValue(headers, "verif-hash")
            ?? GetHeaderValue(headers, "verif_hash");

        if (string.IsNullOrWhiteSpace(headerValue))
            return false;

        var headerBytes = Encoding.UTF8.GetBytes(headerValue.Trim());
        var secretBytes = Encoding.UTF8.GetBytes(secret.Trim());

        return CryptographicOperations.FixedTimeEquals(headerBytes, secretBytes);
    }

    private static bool VerifyPaystack(string rawPayload, IReadOnlyDictionary<string, string> headers, string secret)
    {
        // Paystack sends HMAC-SHA512 signature in 'x-paystack-signature' header
        var headerSignature = GetHeaderValue(headers, "x-paystack-signature");
        if (string.IsNullOrWhiteSpace(headerSignature) || string.IsNullOrEmpty(rawPayload))
            return false;

        var keyBytes = Encoding.UTF8.GetBytes(secret.Trim());
        var payloadBytes = Encoding.UTF8.GetBytes(rawPayload);

        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);
        var computedHex = Convert.ToHexStringLower(hashBytes);

        var headerBytes = Encoding.UTF8.GetBytes(headerSignature.Trim().ToLowerInvariant());
        var computedBytes = Encoding.UTF8.GetBytes(computedHex);

        return CryptographicOperations.FixedTimeEquals(headerBytes, computedBytes);
    }

    private static string? GetHeaderValue(IReadOnlyDictionary<string, string> headers, string key)
    {
        if (headers.TryGetValue(key, out var value))
            return value;

        // Case-insensitive fallback lookup
        foreach (var kvp in headers)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }

        return null;
    }
}
