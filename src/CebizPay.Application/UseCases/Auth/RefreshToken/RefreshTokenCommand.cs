using MediatR;

namespace CebizPay.Application.UseCases.Auth.RefreshToken;

/// <summary>
/// Command to exchange an active refresh token for a new JWT access token and rotated refresh token.
/// </summary>
/// <param name="RefreshToken">The plaintext refresh token string.</param>
/// <param name="IpAddress">Optional client IP address.</param>
public sealed record RefreshTokenCommand(
    string RefreshToken,
    string? IpAddress = null) : IRequest<RefreshTokenResponseDto>;

/// <summary>
/// Response DTO containing the rotated token pair.
/// </summary>
/// <param name="Succeeded">Indicates whether token refresh succeeded.</param>
/// <param name="UserId">User ID associated with the token.</param>
/// <param name="AccessToken">Newly generated JWT access token (15-30 mins).</param>
/// <param name="RefreshToken">Newly rotated refresh token (30-day sliding window).</param>
/// <param name="ErrorMessage">Error message if refresh failed.</param>
public sealed record RefreshTokenResponseDto(
    bool Succeeded,
    string? UserId,
    string? AccessToken,
    string? RefreshToken,
    string? ErrorMessage);
