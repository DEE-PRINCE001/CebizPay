using CebizPay.Domain.Enums;

namespace CebizPay.Application.UseCases.Admin.Manage;

/// <summary>
/// DTO representing an administrative user profile with linked identity information.
/// </summary>
public sealed record AdminProfileDto(
    Guid Id,
    string UserId,
    string Email,
    string? PhoneNumber,
    string Role,
    bool IsActive,
    bool IsMfaEnabled,
    IReadOnlyList<string> Permissions,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>
/// Request to invite a new administrative user.
/// </summary>
public sealed record InviteAdminRequest(
    string Email,
    AdminRoleType Role);

/// <summary>
/// Response DTO after creating an admin invitation, including the single-use 24-hour invitation token.
/// </summary>
public sealed record InviteAdminResponseDto(
    Guid InvitationId,
    string Email,
    string Role,
    string InvitationToken,
    DateTime ExpiresAtUtc);

/// <summary>
/// DTO representing an administrative user invitation.
/// </summary>
public sealed record AdminInvitationDto(
    Guid Id,
    string Email,
    string Role,
    string Status,
    string InvitedByUserId,
    DateTime ExpiresAtUtc,
    DateTime CreatedAtUtc,
    DateTime? RedeemedAtUtc,
    string? RedeemedByUserId);

/// <summary>
/// Request to toggle the active/inactive state of an admin profile.
/// </summary>
public sealed record ToggleAdminStatusRequest(
    Guid AdminProfileId,
    bool IsActive);

/// <summary>
/// Request to redeem an admin invitation and initialize credentials.
/// </summary>
public sealed record RedeemAdminInviteRequest(
    string InvitationToken,
    string Password,
    string? PhoneNumber = null);

/// <summary>
/// Response DTO upon successful redemption of an admin invitation.
/// </summary>
public sealed record RedeemAdminInviteResponseDto(
    bool Succeeded,
    string? UserId,
    string? Email,
    string? Role,
    string? AccessToken,
    string? RefreshToken,
    IEnumerable<string>? Errors);
