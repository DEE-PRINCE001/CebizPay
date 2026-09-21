using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Compliance;

/// <summary>
/// Command to actively synchronize and reconcile KYC verification status when webhooks are dropped or delayed.
/// </summary>
public sealed record SyncKycStatusCommand(
    string ReferenceId,
    string? TargetUserId = null) : IRequest<KycSyncResultDto>;

/// <summary>
/// Handler for actively synchronizing KYC verification status with compliance providers.
/// </summary>
public sealed class SyncKycStatusCommandHandler : IRequestHandler<SyncKycStatusCommand, KycSyncResultDto>
{
    private readonly IVerificationOrchestrator _orchestrator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext? _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="SyncKycStatusCommandHandler"/>.
    /// </summary>
    public SyncKycStatusCommandHandler(
        IVerificationOrchestrator orchestrator,
        ICurrentUserService currentUserService,
        IApplicationDbContext? dbContext = null)
    {
        _orchestrator = orchestrator;
        _currentUserService = currentUserService;
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<KycSyncResultDto> Handle(SyncKycStatusCommand request, CancellationToken cancellationToken)
    {
        await ComplianceSecurityHelper.VerifyTargetUserAccessAsync(request.TargetUserId, _currentUserService, _dbContext, cancellationToken);
        var effectiveUserId = !string.IsNullOrWhiteSpace(request.TargetUserId) ? request.TargetUserId : _currentUserService.UserId;

        if (string.IsNullOrWhiteSpace(effectiveUserId))
            throw new UnauthorizedAccessException("User must be authenticated to synchronize KYC verification status.");

        return await _orchestrator.SyncKycVerificationAsync(effectiveUserId, request.ReferenceId, cancellationToken);
    }
}

/// <summary>
/// Validator for <see cref="SyncKycStatusCommand"/>.
/// </summary>
public sealed class SyncKycStatusCommandValidator : AbstractValidator<SyncKycStatusCommand>
{
    /// <summary>
    /// Initializes a new instance of <see cref="SyncKycStatusCommandValidator"/>.
    /// </summary>
    public SyncKycStatusCommandValidator()
    {
        RuleFor(x => x.ReferenceId)
            .NotEmpty()
            .WithMessage("ReferenceId is required.");
    }
}
