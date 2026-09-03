using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Notifications;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Communication.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Notifications;

/// <summary>
/// Command to orchestrate multi-channel notification dispatch for a single recipient.
/// </summary>
public sealed record DispatchNotificationEventCommand(
    DispatchNotificationRequest Request) : IRequest<MultiChannelDispatchResult>;

/// <summary>
/// Handler for DispatchNotificationEventCommand.
/// </summary>
public sealed class DispatchNotificationEventCommandHandler : IRequestHandler<DispatchNotificationEventCommand, MultiChannelDispatchResult>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly INotificationPolicy _policy;
    private readonly INotificationTemplateEngine _templateEngine;
    private readonly INotificationDeduplicator _deduplicator;
    private readonly INotificationRateLimiter _rateLimiter;
    private readonly IInAppNotificationChannel _inAppChannel;
    private readonly IPushNotificationChannel _pushChannel;
    private readonly IEmailNotificationChannel _emailChannel;
    private readonly ISmsNotificationChannel _smsChannel;

    /// <summary>
    /// Initializes a new instance of <see cref="DispatchNotificationEventCommandHandler"/>.
    /// </summary>
    public DispatchNotificationEventCommandHandler(
        IApplicationDbContext dbContext,
        INotificationPolicy policy,
        INotificationTemplateEngine templateEngine,
        INotificationDeduplicator deduplicator,
        INotificationRateLimiter rateLimiter,
        IInAppNotificationChannel inAppChannel,
        IPushNotificationChannel pushChannel,
        IEmailNotificationChannel emailChannel,
        ISmsNotificationChannel smsChannel)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _templateEngine = templateEngine ?? throw new ArgumentNullException(nameof(templateEngine));
        _deduplicator = deduplicator ?? throw new ArgumentNullException(nameof(deduplicator));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _inAppChannel = inAppChannel ?? throw new ArgumentNullException(nameof(inAppChannel));
        _pushChannel = pushChannel ?? throw new ArgumentNullException(nameof(pushChannel));
        _emailChannel = emailChannel ?? throw new ArgumentNullException(nameof(emailChannel));
        _smsChannel = smsChannel ?? throw new ArgumentNullException(nameof(smsChannel));
    }

    /// <inheritdoc/>
    public async Task<MultiChannelDispatchResult> Handle(DispatchNotificationEventCommand command, CancellationToken cancellationToken)
    {
        var req = command.Request;

        // Fetch user preferences for this notification type
        var preference = await _dbContext.UserNotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == req.RecipientUserId && p.Type == req.Type, cancellationToken);

        // Resolve eligible channels
        var eligibleChannels = _policy.ResolveEligibleChannels(req.Type, req.Priority, preference);
        var channelResults = new List<NotificationDeliveryResult>();

        foreach (var channel in eligibleChannels)
        {
            try
            {
                // 1. Idempotency / Deduplication check
                var shouldDispatch = await _deduplicator.ShouldDispatchAsync(
                    req.EventId,
                    req.RecipientUserId,
                    req.Type,
                    channel,
                    cancellationToken);

                if (!shouldDispatch)
                {
                    channelResults.Add(new NotificationDeliveryResult(channel, NotificationDeliveryStatus.Duplicate, "Event already processed for recipient and channel."));
                    continue;
                }

                // 2. Anti-flooding / Rate limiting check
                var withinRateLimit = await _rateLimiter.ShouldAllowDispatchAsync(
                    req.RecipientUserId,
                    channel,
                    req.Priority,
                    cancellationToken);

                if (!withinRateLimit)
                {
                    await _deduplicator.RecordOutcomeAsync(
                        req.EventId,
                        req.RecipientUserId,
                        req.Type,
                        channel,
                        NotificationDeliveryStatus.Throttled,
                        "Recipient rate limit reached.",
                        cancellationToken);

                    channelResults.Add(new NotificationDeliveryResult(channel, NotificationDeliveryStatus.Throttled, "Rate limit exceeded."));
                    continue;
                }

                // 3. Render safe notification content
                var rendered = _templateEngine.Render(req.Type, channel, req.TemplateParameters);

                var payload = new NotificationPayload(
                    req.EventId,
                    req.RecipientUserId,
                    req.RecipientEmail,
                    req.RecipientPhoneNumber,
                    req.OrganizationId,
                    req.Type,
                    req.Priority,
                    rendered.Title,
                    rendered.Body,
                    rendered.DeepLink,
                    rendered.Metadata);

                // 4. Dispatch via provider-neutral channel
                NotificationDeliveryResult result = channel switch
                {
                    NotificationChannel.InApp => await _inAppChannel.DispatchAsync(payload, cancellationToken),
                    NotificationChannel.Push => await _pushChannel.DispatchAsync(payload, cancellationToken),
                    NotificationChannel.Email => await _emailChannel.DispatchAsync(payload, cancellationToken),
                    NotificationChannel.Sms => await _smsChannel.DispatchAsync(payload, cancellationToken),
                    _ => new NotificationDeliveryResult(channel, NotificationDeliveryStatus.Failed, "Unsupported channel.")
                };

                // 5. Authoritative outcome recording in PostgreSQL
                await _deduplicator.RecordOutcomeAsync(
                    req.EventId,
                    req.RecipientUserId,
                    req.Type,
                    channel,
                    result.Status,
                    result.FailureReason,
                    cancellationToken);

                channelResults.Add(result);
            }
            catch (Exception ex)
            {
                await _deduplicator.RecordOutcomeAsync(
                    req.EventId,
                    req.RecipientUserId,
                    req.Type,
                    channel,
                    NotificationDeliveryStatus.Failed,
                    ex.Message,
                    cancellationToken);

                channelResults.Add(new NotificationDeliveryResult(channel, NotificationDeliveryStatus.Failed, ex.Message));
            }
        }

        return new MultiChannelDispatchResult(req.EventId, req.RecipientUserId, req.Type, channelResults);
    }
}
