using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Events;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Individuals.UpdateKycStatus;

/// <summary>
/// Command to update an individual user's KYC status (Pending, Verified, Rejected).
/// </summary>
public sealed record UpdateKycStatusCommand(
    string UserId,
    KycStatus NewStatus,
    string AdminUserId,
    string? Reason = null) : IRequest<UpdateKycStatusResponseDto>;

/// <summary>
/// Response DTO for UpdateKycStatus.
/// </summary>
public sealed record UpdateKycStatusResponseDto(
    string UserId,
    string KycStatus,
    string? Reason);

/// <summary>
/// Validator for UpdateKycStatusCommand.
/// </summary>
public sealed class UpdateKycStatusCommandValidator : AbstractValidator<UpdateKycStatusCommand>
{
    /// <summary>
    /// Initializes validation rules for UpdateKycStatusCommand.
    /// </summary>
    public UpdateKycStatusCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("UserId is required.");
        RuleFor(x => x.AdminUserId).NotEmpty().WithMessage("AdminUserId is required.");
        RuleFor(x => x.NewStatus).IsInEnum().WithMessage("Valid KycStatus is required.");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .When(x => x.NewStatus == KycStatus.Rejected)
            .WithMessage("Rejection reason is required when rejecting KYC.");

        RuleFor(x => x)
            .Must(x => x.AdminUserId != x.UserId)
            .WithMessage("Admins cannot review or approve their own KYC status.");
    }
}

/// <summary>
/// Handler for UpdateKycStatusCommand.
/// </summary>
public sealed class UpdateKycStatusCommandHandler : IRequestHandler<UpdateKycStatusCommand, UpdateKycStatusResponseDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IEventPublisher _eventPublisher;

    /// <summary>
    /// Initializes a new instance of <see cref="UpdateKycStatusCommandHandler"/>.
    /// </summary>
    public UpdateKycStatusCommandHandler(IApplicationDbContext dbContext, IEventPublisher eventPublisher)
    {
        _dbContext = dbContext;
        _eventPublisher = eventPublisher;
    }

    /// <inheritdoc/>
    public async Task<UpdateKycStatusResponseDto> Handle(UpdateKycStatusCommand request, CancellationToken cancellationToken)
    {
        if (request.AdminUserId == request.UserId)
        {
            throw new InvalidOperationException("Admins cannot review or approve their own KYC status.");
        }

        var profile = await _dbContext.IndividualProfiles
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException($"Individual profile for user {request.UserId} not found.");

        var oldStatus = profile.KycStatus;
        profile.SetKycStatus(request.NewStatus);

        // Update latest KYC documents for this user
        var latestDoc = await _dbContext.KycDocuments
            .Where(d => d.UserId == request.UserId && d.Status == KycStatus.Pending)
            .OrderByDescending(d => d.SubmittedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestDoc != null)
        {
            if (request.NewStatus == KycStatus.Verified)
            {
                latestDoc.Approve(request.AdminUserId, DateTime.UtcNow);
            }
            else if (request.NewStatus == KycStatus.Rejected)
            {
                latestDoc.Reject(request.AdminUserId, request.Reason ?? "KYC rejected during review.", DateTime.UtcNow);
            }
        }

        // Add audit log entry
        var action = request.NewStatus == KycStatus.Verified ? Domain.Auditing.AuditActions.KycVerified : Domain.Auditing.AuditActions.KycRejected;
        _dbContext.AuditLogs.Add(Domain.Entities.AuditLog.Create(
            actorId: request.AdminUserId,
            action: action,
            resourceType: Domain.Auditing.AuditResourceTypes.KycDocument,
            resourceId: profile.UserId,
            afterJson: request.Reason != null ? System.Text.Json.JsonSerializer.Serialize(new { Reason = request.Reason, KycStatus = profile.KycStatus.ToString() }) : null));

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _eventPublisher.PublishAsync(
            new KycStatusChangedDomainEvent(
                request.UserId, oldStatus, profile.KycStatus, request.Reason, DateTime.UtcNow),
            cancellationToken);

        return new UpdateKycStatusResponseDto(profile.UserId, profile.KycStatus.ToString(), request.Reason);
    }
}
