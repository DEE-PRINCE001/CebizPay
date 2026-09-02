namespace CebizPay.Application.Common.Interfaces.Messaging;

/// <summary>
/// Service interface for dispatching SMS messages (Twilio, Termii, or dev dispatcher).
/// </summary>
public interface ISmsService
{
    /// <summary>
    /// Sends an SMS message to the specified phone number asynchronously.
    /// </summary>
    /// <param name="toPhoneNumber">Recipient phone number in E.164 or national format.</param>
    /// <param name="message">SMS message text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the SMS was successfully dispatched/accepted; otherwise false.</returns>
    Task<bool> SendSmsAsync(
        string toPhoneNumber,
        string message,
        CancellationToken cancellationToken = default);
}
