using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Notifications;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Communication.Enums;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Notifications.Channels;

/// <summary>
/// SMS notification channel that dispatches SMS messages via the authoritative <see cref="ISmsService"/> abstraction.
/// Reserved strictly for critical security alerts and organization suspension events.
/// </summary>
public sealed partial class TwilioSmsNotificationChannel : ISmsNotificationChannel
{
    private readonly ISmsService _smsService;
    private readonly CebizPay.Infrastructure.Persistence.ApplicationDbContext _dbContext;
    private readonly ILogger<TwilioSmsNotificationChannel> _logger;

    /// <inheritdoc/>
    public NotificationChannel Channel => NotificationChannel.Sms;

    /// <summary>
    /// Initializes a new instance of <see cref="TwilioSmsNotificationChannel"/>.
    /// </summary>
    public TwilioSmsNotificationChannel(
        ISmsService smsService,
        CebizPay.Infrastructure.Persistence.ApplicationDbContext dbContext,
        ILogger<TwilioSmsNotificationChannel> logger)
    {
        _smsService = smsService ?? throw new ArgumentNullException(nameof(smsService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<NotificationDeliveryResult> DispatchAsync(NotificationPayload payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        try
        {
            var phone = payload.RecipientPhoneNumber;

            // Resolve phone from ASP.NET Identity user if not explicitly populated in payload
            if (string.IsNullOrWhiteSpace(phone))
            {
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == payload.RecipientUserId, cancellationToken);
                phone = user?.PhoneNumber;
            }

            if (string.IsNullOrWhiteSpace(phone))
            {
                LogNoPhoneForUser(_logger, payload.RecipientUserId);
                return new NotificationDeliveryResult(NotificationChannel.Sms, NotificationDeliveryStatus.Delivered, "No phone number found for recipient.");
            }

            // Keep SMS text concise: "CebizPay: {Title} - {Body}"
            var text = $"CebizPay: {payload.Title} - {payload.Body}";
            if (text.Length > 160)
            {
                text = text[..157] + "...";
            }

            var succeeded = await _smsService.SendSmsAsync(
                toPhoneNumber: phone,
                message: text,
                cancellationToken: cancellationToken);

            return succeeded
                ? new NotificationDeliveryResult(NotificationChannel.Sms, NotificationDeliveryStatus.Delivered)
                : new NotificationDeliveryResult(NotificationChannel.Sms, NotificationDeliveryStatus.Failed, "SMS provider returned delivery failure.");
        }
        catch (Exception ex)
        {
            LogSmsDispatchError(_logger, payload.RecipientUserId, ex);
            return new NotificationDeliveryResult(NotificationChannel.Sms, NotificationDeliveryStatus.Failed, ex.Message);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "No recipient phone number available for user {UserId}. Skipping SMS dispatch.")]
    private static partial void LogNoPhoneForUser(ILogger logger, string userId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Failed to dispatch SMS notification to user {UserId}")]
    private static partial void LogSmsDispatchError(ILogger logger, string userId, Exception exception);
}
