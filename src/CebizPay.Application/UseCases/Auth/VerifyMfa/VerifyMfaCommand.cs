using CebizPay.Application.Common.Interfaces.Security;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Auth.VerifyMfa;

/// <summary>
/// Command to verify an MFA challenge and issue JWT tokens upon success.
/// </summary>
public sealed record VerifyMfaCommand(
    Guid ChallengeId,
    string Code) : IRequest<VerifyMfaResponseDto>;

/// <summary>
/// Response DTO for VerifyMfaCommand.
/// </summary>
public sealed record VerifyMfaResponseDto(
    bool Succeeded,
    string UserId,
    string AccessToken,
    string RefreshToken,
    IReadOnlyList<string> Errors);

/// <summary>
/// Validator for VerifyMfaCommand.
/// </summary>
public sealed class VerifyMfaCommandValidator : AbstractValidator<VerifyMfaCommand>
{
    /// <summary>
    /// Initializes validation rules for VerifyMfaCommand.
    /// </summary>
    public VerifyMfaCommandValidator()
    {
        RuleFor(x => x.ChallengeId).NotEmpty().WithMessage("ChallengeId is required.");
        RuleFor(x => x.Code).NotEmpty().WithMessage("MFA Code is required.");
    }
}

/// <summary>
/// Handler for VerifyMfaCommand.
/// </summary>
public sealed class VerifyMfaCommandHandler : IRequestHandler<VerifyMfaCommand, VerifyMfaResponseDto>
{
    private readonly IMfaService _mfaService;
    private readonly IIdentityService _identityService;

    /// <summary>
    /// Initializes a new instance of <see cref="VerifyMfaCommandHandler"/>.
    /// </summary>
    public VerifyMfaCommandHandler(IMfaService mfaService, IIdentityService identityService)
    {
        _mfaService = mfaService;
        _identityService = identityService;
    }

    /// <inheritdoc/>
    public async Task<VerifyMfaResponseDto> Handle(VerifyMfaCommand request, CancellationToken cancellationToken)
    {
        var (succeeded, userId, errors) = await _mfaService.VerifyChallengeAsync(request.ChallengeId, request.Code, cancellationToken);
        if (!succeeded || string.IsNullOrEmpty(userId))
        {
            return new VerifyMfaResponseDto(false, string.Empty, string.Empty, string.Empty, errors.ToList());
        }

        var (accessToken, refreshToken) = await _identityService.IssueTokensForUserAsync(userId, cancellationToken);

        return new VerifyMfaResponseDto(true, userId, accessToken, refreshToken, Array.Empty<string>());
    }
}
