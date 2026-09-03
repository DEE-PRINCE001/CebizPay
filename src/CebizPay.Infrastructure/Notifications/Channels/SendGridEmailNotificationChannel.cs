using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Notifications;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Communication.Enums;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Notifications.Channels;

/// <summary>
/// Email notification channel that dispatches emails via the authoritative <see cref="IEmailService"/> abstraction.
/// </summary>
public sealed partial class SendGridEmailNotificationChannel : IEmailNotificationChannel
{
    private readonly IEmailService _emailService;
    private readonly CebizPay.Infrastructure.Persistence.ApplicationDbContext _dbContext;
    private readonly ILogger<SendGridEmailNotificationChannel> _logger;

    /// <inheritdoc/>
    public NotificationChannel Channel => NotificationChannel.Email;

    /// <summary>
    /// Initializes a new instance of <see cref="SendGridEmailNotificationChannel"/>.
    /// </summary>
    public SendGridEmailNotificationChannel(
        IEmailService emailService,
        CebizPay.Infrastructure.Persistence.ApplicationDbContext dbContext,
        ILogger<SendGridEmailNotificationChannel> logger)
    {
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<NotificationDeliveryResult> DispatchAsync(NotificationPayload payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        try
        {
            var email = payload.RecipientEmail;

            // Resolve email from ASP.NET Identity user if not explicitly populated in payload
            if (string.IsNullOrWhiteSpace(email))
            {
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == payload.RecipientUserId, cancellationToken);
                email = user?.Email;
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                LogNoEmailForUser(_logger, payload.RecipientUserId);
                return new NotificationDeliveryResult(NotificationChannel.Email, NotificationDeliveryStatus.Delivered, "No email address found for recipient.");
            }

            var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f8fafc; color: #1e293b; padding: 24px; }}
        .card {{ max-width: 560px; margin: 0 auto; background: #ffffff; border-radius: 12px; border: 1px solid #e2e8f0; padding: 32px; }}
        .header {{ font-size: 20px; font-weight: bold; color: #0f172a; margin-bottom: 16px; }}
        .body {{ font-size: 14px; line-height: 1.6; color: #334155; margin-bottom: 24px; }}
        .footer {{ font-size: 12px; color: #64748b; border-top: 1px solid #e2e8f0; padding-top: 16px; }}
    </style>
</head>
<body>
    <div class='card'>
        <div class='header'>{payload.Title}</div>
        <div class='body'>{payload.Body}</div>
        <div class='footer'>This is an automated notification from CebizPay. Please do not reply directly to this email.</div>
    </div>
</body>
</html>";

            var succeeded = await _emailService.SendEmailAsync(
                toEmail: email,
                subject: payload.Title,
                htmlBody: htmlBody,
                plainTextBody: payload.Body,
                cancellationToken: cancellationToken);

            return succeeded
                ? new NotificationDeliveryResult(NotificationChannel.Email, NotificationDeliveryStatus.Delivered)
                : new NotificationDeliveryResult(NotificationChannel.Email, NotificationDeliveryStatus.Failed, "Email provider returned delivery failure.");
        }
        catch (Exception ex)
        {
            LogEmailDispatchError(_logger, payload.RecipientUserId, ex);
            return new NotificationDeliveryResult(NotificationChannel.Email, NotificationDeliveryStatus.Failed, ex.Message);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "No recipient email address available for user {UserId}. Skipping email dispatch.")]
    private static partial void LogNoEmailForUser(ILogger logger, string userId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Failed to dispatch email notification to user {UserId}")]
    private static partial void LogEmailDispatchError(ILogger logger, string userId, Exception exception);
}
