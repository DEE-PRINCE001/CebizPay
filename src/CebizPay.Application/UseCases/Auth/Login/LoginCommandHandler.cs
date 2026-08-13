using CebizPay.Application.Common.Interfaces.Security;
using MediatR;

namespace CebizPay.Application.UseCases.Auth.Login;

/// <summary>
/// Handler for executing LoginCommand.
/// </summary>
public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponseDto>
{
    private readonly IIdentityService _identityService;

    /// <summary>
    /// Initializes a new instance of <see cref="LoginCommandHandler"/>.
    /// </summary>
    public LoginCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    /// <inheritdoc/>
    public async Task<LoginResponseDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var result = await _identityService.LoginAsync(request.Email, request.Password, cancellationToken);
        if (!result.Succeeded)
        {
            return new LoginResponseDto(false, null, null, null, result.Errors);
        }

        return new LoginResponseDto(true, result.UserId, result.AccessToken, result.RefreshToken, null);
    }
}
