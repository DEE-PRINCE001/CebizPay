namespace CebizPay.Application.Common.Interfaces.Security;

/// <summary>
/// Abstraction for delivering MFA challenge codes to the user through the configured factor channel.
/// The concrete implementation is determined by the production MFA factor decision
/// (e.g., email OTP, SMS OTP, TOTP). Only the infrastructure layer implements this interface.
/// The MFA code must never be exposed through the API, logs, or telemetry.
/// </summary>
public interface IMfaCodeDeliveryService
{
    /// <summary>
    /// Delivers the MFA challenge code to the user through the configured channel.
    /// The plain code is passed here only for delivery; it is not stored in plain form and
    /// must not be returned to any API caller.
    /// </summary>
    /// <param name="userId">The user ID for whom the challenge was created.</param>
    /// <param name="plainCode">The raw 6-digit code to deliver. Must not be logged or propagated further.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeliverAsync(string userId, string plainCode, CancellationToken cancellationToken = default);
}
