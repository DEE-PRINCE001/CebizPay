using CebizPay.Application.Common.Interfaces.Security;
using MediatR;

namespace CebizPay.Application.UseCases.Auth.RefreshToken;

/// <summary>
/// Handler for RefreshTokenCommand that validates and rotates refresh tokens.
/// </summary>
public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, RefreshTokenResponseDto>
{
    private readonly IIdentityService _identityService;

    /// <summary>
    /// Initializes a new instance of <see cref="RefreshTokenCommandHandler"/>.
    /// </summary>
    public RefreshTokenCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
    }

    /// <inheritdoc/>
    public async Task<RefreshTokenResponseDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var (succeeded, userId, accessToken, newRefreshToken, errorMessage) =
            await _identityService.RefreshTokenAsync(request.RefreshToken, request.IpAddress, cancellationToken);

        if (!succeeded)
        {
            return new RefreshTokenResponseDto(false, null, null, null, errorMessage ?? "Invalid or expired refresh token.");
        }

        return new RefreshTokenResponseDto(true, userId, accessToken, newRefreshToken, null);
    }
}
