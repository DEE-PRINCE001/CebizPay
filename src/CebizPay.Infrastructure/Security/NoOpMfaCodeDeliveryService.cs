using CebizPay.Application.Common.Interfaces.Security;

namespace CebizPay.Infrastructure.Security;

/// <summary>
/// Default production implementation of <see cref="IMfaCodeDeliveryService"/>.
/// Acts as a no-op until the concrete MFA factor (email OTP, SMS OTP, TOTP, etc.) is selected
/// and wired in by the team. The concrete factor decision is a product/infrastructure decision
/// that has not been made yet (see Phase 1 stop rule).
/// </summary>
/// <remarks>
/// Replace this with a concrete delivery implementation once a factor is chosen.
/// The interface boundary ensures the rest of the system is already correctly decoupled.
/// </remarks>
public sealed class NoOpMfaCodeDeliveryService : IMfaCodeDeliveryService
{
    /// <inheritdoc/>
    /// <remarks>
    /// No-op: the MFA factor channel has not yet been configured.
    /// The code is received here only to satisfy the delivery contract —
    /// it must NOT be logged, stored, or propagated anywhere.
    /// </remarks>
    public Task DeliverAsync(string userId, string plainCode, CancellationToken cancellationToken = default)
    {
        // IMPORTANT: Do NOT log, store, or propagate the plainCode parameter.
        // The concrete delivery implementation (email/SMS/TOTP) will be registered here
        // once the factor decision is made.
        return Task.CompletedTask;
    }
}
