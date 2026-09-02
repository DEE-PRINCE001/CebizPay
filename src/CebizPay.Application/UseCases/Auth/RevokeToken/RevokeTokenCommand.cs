using CebizPay.Application.Common.Interfaces.Security;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Auth.RevokeToken;

/// <summary>
/// Command to explicitly revoke a refresh token (e.g. on user logout).
/// </summary>
/// <param name="RefreshToken">The plaintext refresh token string to revoke.</param>
public sealed record RevokeTokenCommand(string RefreshToken) : IRequest<RevokeTokenResponseDto>;

/// <summary>
/// Response DTO for token revocation.
/// </summary>
/// <param name="Succeeded">Indicates whether revocation succeeded.</param>
/// <param name="Message">Status description message.</param>
public sealed record RevokeTokenResponseDto(bool Succeeded, string Message);

/// <summary>
/// Validator for RevokeTokenCommand.
/// </summary>
public sealed class RevokeTokenCommandValidator : AbstractValidator<RevokeTokenCommand>
{
    /// <summary>
    /// Initializes validation rules for RevokeTokenCommand.
    /// </summary>
    public RevokeTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("RefreshToken is required.");
    }
}

/// <summary>
/// Handler for RevokeTokenCommand.
/// </summary>
public sealed class RevokeTokenCommandHandler : IRequestHandler<RevokeTokenCommand, RevokeTokenResponseDto>
{
    private readonly IIdentityService _identityService;

    /// <summary>
    /// Initializes a new instance of <see cref="RevokeTokenCommandHandler"/>.
    /// </summary>
    public RevokeTokenCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
    }

    /// <inheritdoc/>
    public async Task<RevokeTokenResponseDto> Handle(RevokeTokenCommand request, CancellationToken cancellationToken)
    {
        var succeeded = await _identityService.RevokeRefreshTokenAsync(request.RefreshToken, cancellationToken);
        if (!succeeded)
        {
            return new RevokeTokenResponseDto(false, "Token could not be revoked or was not found.");
        }

        return new RevokeTokenResponseDto(true, "Token revoked successfully.");
    }
}
