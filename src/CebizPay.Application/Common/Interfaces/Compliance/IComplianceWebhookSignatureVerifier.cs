using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Application.Common.Interfaces.Compliance;

/// <summary>
/// Cryptographic signature verifier for inbound compliance provider webhooks.
/// </summary>
public interface IComplianceWebhookSignatureVerifier
{
    /// <summary>
    /// Verifies the cryptographic signature of an inbound compliance webhook payload.
    /// </summary>
    bool VerifySignature(
        VerificationProvider provider,
        string rawPayload,
        IReadOnlyDictionary<string, string> headers,
        string secret);
}
