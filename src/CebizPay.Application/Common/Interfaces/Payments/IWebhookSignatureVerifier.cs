using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Application.Common.Interfaces.Payments;

/// <summary>
/// Verifies cryptographic signatures and authorization headers for provider webhooks.
/// </summary>
public interface IWebhookSignatureVerifier
{
    /// <summary>
    /// Verifies the authenticity of an incoming webhook payload using the provider's signature algorithm.
    /// </summary>
    /// <param name="provider">The payment provider issuing the webhook.</param>
    /// <param name="rawPayload">The exact UTF-8 raw request payload.</param>
    /// <param name="headers">HTTP headers from the webhook request.</param>
    /// <param name="secret">The expected webhook secret or secret hash.</param>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
    bool VerifySignature(
        PaymentProvider provider,
        string rawPayload,
        IReadOnlyDictionary<string, string> headers,
        string secret);
}
