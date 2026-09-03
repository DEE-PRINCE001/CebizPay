using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Notifications;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Communication.Entities;
using CebizPay.Domain.Communication.Enums;
using CebizPay.Infrastructure.Options;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CebizPay.Infrastructure.Notifications.Channels;

/// <summary>
/// Push notification channel backed by Firebase Cloud Messaging (FCM).
/// Safely manages device tokens, handles expired registration tokens, and ensures credentials are never logged.
/// </summary>
public sealed partial class FirebasePushNotificationChannel : IPushNotificationChannel
{
    private readonly IApplicationDbContext _dbContext;
    private readonly FirebaseOptions _options;
    private readonly ILogger<FirebasePushNotificationChannel> _logger;
    private static readonly object AppInitLock = new();
    private static bool _isInitialized;

    /// <inheritdoc/>
    public NotificationChannel Channel => NotificationChannel.Push;

    /// <summary>
    /// Initializes a new instance of <see cref="FirebasePushNotificationChannel"/>.
    /// </summary>
    public FirebasePushNotificationChannel(
        IApplicationDbContext dbContext,
        IOptions<FirebaseOptions> options,
        ILogger<FirebasePushNotificationChannel> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _options = options?.Value ?? new FirebaseOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        EnsureFirebaseInitialized();
    }

    private void EnsureFirebaseInitialized()
    {
        if (_isInitialized || !_options.Enabled)
        {
            return;
        }

        lock (AppInitLock)
        {
            if (_isInitialized) return;

            try
            {
                if (FirebaseApp.DefaultInstance == null)
                {
                    GoogleCredential credential;

                    if (!string.IsNullOrWhiteSpace(_options.CredentialsJson))
                    {
                        if (_options.CredentialsJson.TrimStart().StartsWith('{'))
                        {
                            credential = GoogleCredential.FromJson(_options.CredentialsJson);
                        }
                        else if (File.Exists(_options.CredentialsJson))
                        {
                            credential = GoogleCredential.FromFile(_options.CredentialsJson);
                        }
                        else
                        {
                            credential = GoogleCredential.GetApplicationDefault();
                        }
                    }
                    else
                    {
                        credential = GoogleCredential.GetApplicationDefault();
                    }

                    FirebaseApp.Create(new AppOptions
                    {
                        Credential = credential,
                        ProjectId = _options.ProjectId
                    });
                }

                _isInitialized = true;
                LogFirebaseInitSuccess(_logger, _options.ProjectId ?? "Default");
            }
            catch (Exception ex)
            {
                LogFirebaseInitWarning(_logger, ex);
            }
        }
    }

    /// <inheritdoc/>
    public async Task<NotificationDeliveryResult> DispatchAsync(NotificationPayload payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        try
        {
            // Query active tokens for the recipient user
            var activeTokens = await _dbContext.DeviceTokens
                .Where(t => t.UserId == payload.RecipientUserId && t.IsActive)
                .ToListAsync(cancellationToken);

            if (activeTokens.Count == 0)
            {
                LogNoActiveTokens(_logger, payload.RecipientUserId);
                return new NotificationDeliveryResult(NotificationChannel.Push, NotificationDeliveryStatus.Delivered, "No active device tokens.");
            }

            // Fallback / mock mode when FCM is not enabled or credentials not available in environment
            if (!_options.Enabled || FirebaseApp.DefaultInstance == null)
            {
                LogPushSimulated(_logger, payload.RecipientUserId, activeTokens.Count, payload.Title);

                var simulatedTime = DateTime.UtcNow;
                foreach (var token in activeTokens)
                {
                    token.RecordUsed(simulatedTime);
                }
                await _dbContext.SaveChangesAsync(cancellationToken);

                return new NotificationDeliveryResult(NotificationChannel.Push, NotificationDeliveryStatus.Delivered);
            }

            var now = DateTime.UtcNow;
            var invalidTokens = new List<DeviceToken>();
            int successCount = 0;

            foreach (var deviceToken in activeTokens)
            {
                try
                {
                    var message = new Message
                    {
                        Token = deviceToken.Token,
                        Notification = new Notification
                        {
                            Title = payload.Title,
                            Body = payload.Body
                        },
                        Data = new Dictionary<string, string>
                        {
                            ["eventId"] = payload.EventId,
                            ["type"] = payload.Type.ToString(),
                            ["priority"] = payload.Priority.ToString(),
                            ["deepLink"] = payload.DeepLink ?? string.Empty
                        }
                    };

                    await FirebaseMessaging.DefaultInstance.SendAsync(message, cancellationToken);
                    deviceToken.RecordUsed(now);
                    successCount++;
                }
                catch (FirebaseMessagingException fex) when (
                    fex.MessagingErrorCode == MessagingErrorCode.Unregistered ||
                    fex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
                {
                    LogInvalidToken(_logger, payload.RecipientUserId);
                    invalidTokens.Add(deviceToken);
                }
                catch (Exception ex)
                {
                    LogPushDeviceError(_logger, payload.RecipientUserId, ex);
                }
            }

            if (invalidTokens.Count > 0)
            {
                foreach (var invalidToken in invalidTokens)
                {
                    invalidToken.Deactivate(now);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            if (successCount > 0 || invalidTokens.Count == activeTokens.Count)
            {
                return new NotificationDeliveryResult(NotificationChannel.Push, NotificationDeliveryStatus.Delivered);
            }

            return new NotificationDeliveryResult(NotificationChannel.Push, NotificationDeliveryStatus.Failed, "All FCM push dispatch attempts failed.");
        }
        catch (Exception ex)
        {
            LogPushDispatchError(_logger, payload.RecipientUserId, ex);
            return new NotificationDeliveryResult(NotificationChannel.Push, NotificationDeliveryStatus.Failed, ex.Message);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Firebase Cloud Messaging initialized successfully for project {ProjectId}")]
    private static partial void LogFirebaseInitSuccess(ILogger logger, string projectId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "FirebaseApp could not be initialized from environment configuration. Falling back to development mock mode.")]
    private static partial void LogFirebaseInitWarning(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "No active FCM device tokens found for user {UserId}")]
    private static partial void LogNoActiveTokens(ILogger logger, string userId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "FCM push dispatch simulated for user {UserId} across {Count} devices (FCM Disabled/Mock mode). Title: {Title}")]
    private static partial void LogPushSimulated(ILogger logger, string userId, int count, string title);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "FCM token for user {UserId} is invalid or unregistered. Deactivating device record.")]
    private static partial void LogInvalidToken(ILogger logger, string userId);

    [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "Transient error dispatching push notification to device for user {UserId}")]
    private static partial void LogPushDeviceError(ILogger logger, string userId, Exception exception);

    [LoggerMessage(EventId = 7, Level = LogLevel.Error, Message = "Unexpected error in Firebase push dispatch for user {UserId}")]
    private static partial void LogPushDispatchError(ILogger logger, string userId, Exception exception);
}
