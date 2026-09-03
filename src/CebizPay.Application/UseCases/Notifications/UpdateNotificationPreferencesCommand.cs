using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Communication.Entities;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Notifications;

/// <summary>
/// Command to update user channel delivery preferences.
/// </summary>
public sealed record UpdateNotificationPreferencesCommand(
    List<UpdatePreferenceItem> Preferences) : IRequest<List<NotificationPreferenceDto>>;

/// <summary>
/// Validator for UpdateNotificationPreferencesCommand.
/// </summary>
public sealed class UpdateNotificationPreferencesCommandValidator : AbstractValidator<UpdateNotificationPreferencesCommand>
{
    /// <summary>
    /// Initializes validation rules for UpdateNotificationPreferencesCommand.
    /// </summary>
    public UpdateNotificationPreferencesCommandValidator()
    {
        RuleFor(x => x.Preferences)
            .NotNull().WithMessage("Preferences list cannot be null.");
    }
}

/// <summary>
/// Handler for UpdateNotificationPreferencesCommand.
/// </summary>
public sealed class UpdateNotificationPreferencesCommandHandler : IRequestHandler<UpdateNotificationPreferencesCommand, List<NotificationPreferenceDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="UpdateNotificationPreferencesCommandHandler"/>.
    /// </summary>
    public UpdateNotificationPreferencesCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<List<NotificationPreferenceDto>> Handle(UpdateNotificationPreferencesCommand request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var existing = await _dbContext.UserNotificationPreferences
            .Where(p => p.UserId == callerUserId)
            .ToListAsync(cancellationToken);

        var map = existing.ToDictionary(p => p.Type);
        var now = DateTime.UtcNow;

        foreach (var item in request.Preferences ?? Enumerable.Empty<UpdatePreferenceItem>())
        {
            if (map.TryGetValue(item.Type, out var pref))
            {
                pref.Update(inApp: true, push: item.PushEnabled, email: item.EmailEnabled, sms: item.SmsEnabled, now);
            }
            else
            {
                var newPref = UserNotificationPreference.CreateDefault(callerUserId, item.Type);
                newPref.Update(inApp: true, push: item.PushEnabled, email: item.EmailEnabled, sms: item.SmsEnabled, now);
                _dbContext.UserNotificationPreferences.Add(newPref);
                map[item.Type] = newPref;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return map.Values.Select(p => new NotificationPreferenceDto(
            p.Type,
            p.InAppEnabled,
            p.PushEnabled,
            p.EmailEnabled,
            p.SmsEnabled,
            UserNotificationPreference.IsMandatoryCategory(p.Type))).ToList();
    }
}
