using MediatR;

namespace CebizPay.Application.UseCases.Auth.ChangePassword;

/// <summary>
/// Command to change user password.
/// </summary>
/// <param name="UserId">Authenticated user ID.</param>
/// <param name="CurrentPassword">Current password.</param>
/// <param name="NewPassword">New password.</param>
/// <param name="IsMobile">Flag indicating if request comes from mobile device.</param>
public sealed record ChangePasswordCommand(
    string UserId,
    string CurrentPassword,
    string NewPassword,
    bool IsMobile) : IRequest<ChangePasswordResponseDto>;

/// <summary>
/// Response DTO for password change.
/// </summary>
/// <param name="Succeeded">Indicates if password change succeeded.</param>
/// <param name="Errors">Error messages if failed.</param>
public sealed record ChangePasswordResponseDto(
    bool Succeeded,
    IEnumerable<string>? Errors);
