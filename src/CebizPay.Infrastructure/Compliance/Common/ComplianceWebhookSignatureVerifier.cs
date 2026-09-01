#pragma warning disable CA1848, CA1873, CA1305, CS1591
using System.Security.Cryptography;
using System.Text;
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Domain.Compliance.Enums;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Compliance.Common;

/// <summary>
/// Cryptographic signature verifier for inbound compliance provider webhook callbacks.
/// Utilizes constant-time comparisons to mitigate timing attacks.
/// </summary>
public sealed class ComplianceWebhookSignatureVerifier : IComplianceWebhookSignatureVerifier
{
    private readonly ILogger<ComplianceWebhookSignatureVerifier> _logger;

    public ComplianceWebhookSignatureVerifier(ILogger<ComplianceWebhookSignatureVerifier> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool VerifySignature(
        VerificationProvider provider,
        string rawPayload,
        IReadOnlyDictionary<string, string> headers,
        string secret)
    {
        if (string.IsNullOrWhiteSpace(rawPayload) || string.IsNullOrWhiteSpace(secret))
            return false;

        return provider switch
        {
            VerificationProvider.Dojah => VerifyDojahSignature(rawPayload, headers, secret),
            VerificationProvider.SmileId => VerifySmileIdSignature(rawPayload, headers, secret),
            VerificationProvider.Ninja => VerifyNinjaSignature(rawPayload, headers, secret),
            _ => false
        };
    }

    private bool VerifyDojahSignature(string rawPayload, IReadOnlyDictionary<string, string> headers, string secret)
    {
        if (!TryGetHeader(headers, "X-Dojah-Signature", out var signature) &&
            !TryGetHeader(headers, "x-dojah-signature", out signature) &&
            !TryGetHeader(headers, "X-Signature", out signature))
        {
            _logger.LogWarning("Dojah webhook signature header missing.");
            return false;
        }

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawPayload));
        var computedHex = Convert.ToHexString(computedHash);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(signature.ToLowerInvariant()));
    }

    private bool VerifySmileIdSignature(string rawPayload, IReadOnlyDictionary<string, string> headers, string secret)
    {
        if (!TryGetHeader(headers, "X-Smile-Signature", out var signature) &&
            !TryGetHeader(headers, "x-smile-signature", out signature) &&
            !TryGetHeader(headers, "Signature", out signature))
        {
            _logger.LogWarning("Smile ID webhook signature header missing.");
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawPayload));
        var computedBase64 = Convert.ToBase64String(computedHash);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedBase64),
            Encoding.UTF8.GetBytes(signature));
    }

    private bool VerifyNinjaSignature(string rawPayload, IReadOnlyDictionary<string, string> headers, string secret)
    {
        if (!TryGetHeader(headers, "X-Ninja-Signature", out var signature) &&
            !TryGetHeader(headers, "x-ninja-signature", out signature) &&
            !TryGetHeader(headers, "X-Signature", out signature))
        {
            _logger.LogWarning("Ninja webhook signature header missing.");
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawPayload));
        var computedHex = Convert.ToHexString(computedHash);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(signature.ToLowerInvariant()));
    }

    private static bool TryGetHeader(IReadOnlyDictionary<string, string> headers, string headerName, out string value)
    {
        value = string.Empty;
        if (headers == null)
            return false;

        foreach (var kvp in headers)
        {
            if (string.Equals(kvp.Key, headerName, StringComparison.OrdinalIgnoreCase))
            {
                value = kvp.Value;
                return true;
            }
        }

        return false;
    }
}
