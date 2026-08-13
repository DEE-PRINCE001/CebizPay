using CebizPay.Application.Common.Interfaces.Security;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Auth.ToggleMfa;

/// <summary>
/// Command to enable or disable MFA for a user/admin profile.
/// </summary>
public sealed record ToggleMfaCommand(
    string UserId,
    bool Enable) : IRequest<ToggleMfaResponseDto>;

/// <summary>
/// Response DTO for ToggleMfaCommand.
/// </summary>
public sealed record ToggleMfaResponseDto(
    string UserId,
    bool IsMfaEnabled);

/// <summary>
/// Validator for ToggleMfaCommand.
/// </summary>
public sealed class ToggleMfaCommandValidator : AbstractValidator<ToggleMfaCommand>
{
    /// <summary>
    /// Initializes validation rules for ToggleMfaCommand.
    /// </summary>
    public ToggleMfaCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("UserId is required.");
    }
}

/// <summary>
/// Handler for ToggleMfaCommand.
/// </summary>
public sealed class ToggleMfaCommandHandler : IRequestHandler<ToggleMfaCommand, ToggleMfaResponseDto>
{
    private readonly IMfaService _mfaService;

    /// <summary>
    /// Initializes a new instance of <see cref="ToggleMfaCommandHandler"/>.
    /// </summary>
    public ToggleMfaCommandHandler(IMfaService mfaService)
    {
        _mfaService = mfaService;
    }

    /// <inheritdoc/>
    public async Task<ToggleMfaResponseDto> Handle(ToggleMfaCommand request, CancellationToken cancellationToken)
    {
        if (request.Enable)
        {
            await _mfaService.EnableMfaAsync(request.UserId, cancellationToken);
        }
        else
        {
            await _mfaService.DisableMfaAsync(request.UserId, cancellationToken);
        }

        var isEnabled = await _mfaService.IsMfaEnabledAsync(request.UserId, cancellationToken);

        return new ToggleMfaResponseDto(request.UserId, isEnabled);
    }
}
