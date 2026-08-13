using MediatR;

namespace CebizPay.Application.UseCases.Auth.Login;

/// <summary>
/// DTO representing login response.
/// </summary>

/// <summary>
/// Command to log in with email and password.
/// </summary>
/// <param name="Email">User email address.</param>
/// <param name="Password">User password.</param>
public sealed record LoginCommand(
    string Email,
    string Password) : IRequest<LoginResponseDto>;

/// <summary>
/// DTO representing login response payload.
/// </summary>
/// <param name="Succeeded">Indicates if login succeeded.</param>
/// <param name="UserId">Authenticated user ID.</param>
/// <param name="AccessToken">JWT Access Token (15-min lifespan).</param>
/// <param name="RefreshToken">Refresh Token (30-day sliding window).</param>
/// <param name="Errors">List of error messages if authentication failed.</param>
public sealed record LoginResponseDto(
    bool Succeeded,
    string? UserId,
    string? AccessToken,
    string? RefreshToken,
    IEnumerable<string>? Errors);
