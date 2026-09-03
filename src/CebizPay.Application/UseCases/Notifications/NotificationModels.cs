using CebizPay.Domain.Communication.Enums;

namespace CebizPay.Application.UseCases.Notifications;

/// <summary>
/// Data transfer object for in-app notifications.
/// </summary>
public sealed record InAppNotificationDto(
    Guid Id,
    string UserId,
    Guid? OrganizationId,
    NotificationType Type,
    string Title,
    string Body,
    NotificationPriority Priority,
    string? DeepLink,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc,
    bool IsRead);

/// <summary>
/// Data transfer object for user channel delivery preferences.
/// </summary>
public sealed record NotificationPreferenceDto(
    NotificationType Type,
    bool InAppEnabled,
    bool PushEnabled,
    bool EmailEnabled,
    bool SmsEnabled,
    bool IsMandatory);

/// <summary>
/// Request payload to register an FCM device token.
/// </summary>
public sealed record RegisterDeviceTokenRequest(
    string Token,
    DevicePlatform Platform,
    string? DeviceModel = null);

/// <summary>
/// Item representing a preference update for a notification category.
/// </summary>
public sealed record UpdatePreferenceItem(
    NotificationType Type,
    bool PushEnabled,
    bool EmailEnabled,
    bool SmsEnabled);

/// <summary>
/// Request payload to update notification delivery preferences.
/// </summary>
public sealed record UpdateNotificationPreferencesRequest(
    List<UpdatePreferenceItem> Preferences);
