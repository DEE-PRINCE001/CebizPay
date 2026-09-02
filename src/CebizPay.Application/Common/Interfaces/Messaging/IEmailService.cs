namespace CebizPay.Application.Common.Interfaces.Messaging;

/// <summary>
/// Service interface for dispatching transactional emails (SendGrid, SMTP, or dev dispatcher).
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an email message asynchronously.
    /// </summary>
    /// <param name="toEmail">Recipient email address.</param>
    /// <param name="subject">Email subject line.</param>
    /// <param name="htmlBody">HTML body content.</param>
    /// <param name="plainTextBody">Optional plain-text fallback content.</param>
    /// <param name="toName">Optional recipient display name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the email was successfully accepted/dispatched; otherwise false.</returns>
    Task<bool> SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? plainTextBody = null,
        string? toName = null,
        CancellationToken cancellationToken = default);
}
