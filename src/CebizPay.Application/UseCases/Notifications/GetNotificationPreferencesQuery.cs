using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Communication.Entities;
using CebizPay.Domain.Communication.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Notifications;

/// <summary>
/// Query to retrieve a user's delivery preferences across all notification categories.
/// </summary>
public sealed record GetNotificationPreferencesQuery : IRequest<List<NotificationPreferenceDto>>;

/// <summary>
/// Handler for GetNotificationPreferencesQuery.
/// </summary>
public sealed class GetNotificationPreferencesQueryHandler : IRequestHandler<GetNotificationPreferencesQuery, List<NotificationPreferenceDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetNotificationPreferencesQueryHandler"/>.
    /// </summary>
    public GetNotificationPreferencesQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<List<NotificationPreferenceDto>> Handle(GetNotificationPreferencesQuery request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var savedPreferences = await _dbContext.UserNotificationPreferences
            .Where(p => p.UserId == callerUserId)
            .ToListAsync(cancellationToken);

        var prefMap = savedPreferences.ToDictionary(p => p.Type);
        var allTypes = Enum.GetValues<NotificationType>();
        var result = new List<NotificationPreferenceDto>();

        foreach (var type in allTypes)
        {
            var isMandatory = UserNotificationPreference.IsMandatoryCategory(type);

            if (prefMap.TryGetValue(type, out var pref))
            {
                result.Add(new NotificationPreferenceDto(
                    pref.Type,
                    pref.InAppEnabled,
                    pref.PushEnabled,
                    pref.EmailEnabled,
                    pref.SmsEnabled,
                    isMandatory));
            }
            else
            {
                var def = UserNotificationPreference.CreateDefault(callerUserId, type);
                result.Add(new NotificationPreferenceDto(
                    def.Type,
                    def.InAppEnabled,
                    def.PushEnabled,
                    def.EmailEnabled,
                    def.SmsEnabled,
                    isMandatory));
            }
        }

        return result;
    }
}
