using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Application.Common.Interfaces.Compliance;

/// <summary>
/// Service responsible for authenticating, deduplicating, and asynchronously processing
/// inbound compliance verification webhook callbacks from Dojah, Smile ID, and Ninja.
/// </summary>
public interface IComplianceWebhookProcessor
{
    /// <summary>
    /// Authenticates and processes an inbound compliance webhook callback.
    /// </summary>
    Task<ComplianceWebhookProcessingResult> ProcessWebhookAsync(
        VerificationProvider provider,
        string rawPayload,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default);
}
