using CebizPay.Application.Common.Interfaces.Notifications;
using CebizPay.Application.UseCases.Notifications;
using MediatR;

namespace CebizPay.Infrastructure.Notifications;

/// <summary>
/// Infrastructure entry point for multi-channel notification dispatch.
/// Mediates dispatch requests through MediatR pipeline.
/// </summary>
public sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="NotificationDispatcher"/>.
    /// </summary>
    public NotificationDispatcher(ISender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    /// <inheritdoc/>
    public Task<MultiChannelDispatchResult> DispatchAsync(
        DispatchNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _sender.Send(new DispatchNotificationEventCommand(request), cancellationToken);
    }
}
