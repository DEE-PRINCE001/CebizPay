using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Notifications;

/// <summary>
/// Command to mark a specific in-app notification as read.
/// </summary>
public sealed record MarkNotificationReadCommand(
    Guid NotificationId) : IRequest<bool>;

/// <summary>
/// Validator for MarkNotificationReadCommand.
/// </summary>
public sealed class MarkNotificationReadCommandValidator : AbstractValidator<MarkNotificationReadCommand>
{
    /// <summary>
    /// Initializes validation rules for MarkNotificationReadCommand.
    /// </summary>
    public MarkNotificationReadCommandValidator()
    {
        RuleFor(x => x.NotificationId)
            .NotEmpty().WithMessage("NotificationId is required.");
    }
}

/// <summary>
/// Handler for MarkNotificationReadCommand.
/// </summary>
public sealed class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="MarkNotificationReadCommandHandler"/>.
    /// </summary>
    public MarkNotificationReadCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<bool> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var notification = await _dbContext.InAppNotifications
            .FirstOrDefaultAsync(n => n.Id == request.NotificationId && n.UserId == callerUserId, cancellationToken)
            ?? throw new KeyNotFoundException($"Notification '{request.NotificationId}' not found.");

        notification.MarkAsRead(DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
