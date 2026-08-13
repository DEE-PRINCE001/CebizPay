using MediatR;

namespace CebizPay.Application.UseCases.Auth.VerifyOtp;

/// <summary>
/// Command to verify mobile OTP and complete user registration.
/// </summary>
/// <param name="Phone">Phone number.</param>
/// <param name="Code">OTP verification code.</param>
/// <param name="Email">User email.</param>
/// <param name="Password">User password.</param>
/// <param name="FirstName">First name.</param>
/// <param name="LastName">Last name.</param>
public sealed record VerifyOtpCommand(
    string Phone,
    string Code,
    string Email,
    string Password,
    string FirstName,
    string LastName) : IRequest<VerifyOtpResponseDto>;

/// <summary>
/// Response DTO for OTP verification and registration completion.
/// </summary>
/// <param name="Success">Indicates whether registration succeeded.</param>
/// <param name="UserId">Created user ID.</param>
/// <param name="AccessToken">Access token.</param>
/// <param name="RefreshToken">Refresh token.</param>
/// <param name="Errors">Error details if failed.</param>
public sealed record VerifyOtpResponseDto(
    bool Success,
    string? UserId,
    string? AccessToken,
    string? RefreshToken,
    IEnumerable<string>? Errors);
