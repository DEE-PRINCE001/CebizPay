using CebizPay.Application.Common.Interfaces.Security;
using MediatR;

namespace CebizPay.Application.UseCases.Auth.ChangePassword;

/// <summary>
/// Handler for ChangePasswordCommand.
/// </summary>
public sealed class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, ChangePasswordResponseDto>
{
    private readonly IIdentityService _identityService;

    /// <summary>
    /// Initializes a new instance of <see cref="ChangePasswordCommandHandler"/>.
    /// </summary>
    public ChangePasswordCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    /// <inheritdoc/>
    public async Task<ChangePasswordResponseDto> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var result = await _identityService.ChangePasswordAsync(
            request.UserId,
            request.CurrentPassword,
            request.NewPassword,
            request.IsMobile,
            cancellationToken);

        return new ChangePasswordResponseDto(result.Succeeded, result.Errors);
    }
}
