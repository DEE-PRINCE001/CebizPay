using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Events;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Application.UseCases.Organizations.ReviewKyb;

/// <summary>
/// Command to review and verify/reject an organization's KYB submission.
/// </summary>
public sealed record ReviewKybCommand(
    Guid OrganizationId,
    KybStatus NewStatus,
    string AdminUserId,
    string? Reason = null) : IRequest<ReviewKybResponseDto>;

/// <summary>
/// Response DTO for ReviewKybCommand.
/// </summary>
public sealed record ReviewKybResponseDto(
    Guid OrganizationId,
    string KybStatus,
    string OrganizationStatus,
    string? Reason);

/// <summary>
/// Validator for ReviewKybCommand.
/// </summary>
public sealed class ReviewKybCommandValidator : AbstractValidator<ReviewKybCommand>
{
    /// <summary>
    /// Initializes validation rules for ReviewKybCommand.
    /// </summary>
    public ReviewKybCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.AdminUserId).NotEmpty().WithMessage("AdminUserId is required.");
        RuleFor(x => x.NewStatus).IsInEnum().WithMessage("Valid KybStatus is required.");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .When(x => x.NewStatus == KybStatus.Rejected)
            .WithMessage("Rejection reason is required when rejecting KYB.");
    }
}

/// <summary>
/// Handler for ReviewKybCommand.
/// </summary>
public sealed class ReviewKybCommandHandler : IRequestHandler<ReviewKybCommand, ReviewKybResponseDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IEventPublisher _eventPublisher;

    /// <summary>
    /// Initializes a new instance of <see cref="ReviewKybCommandHandler"/>.
    /// </summary>
    public ReviewKybCommandHandler(IApplicationDbContext dbContext, IEventPublisher eventPublisher)
    {
        _dbContext = dbContext;
        _eventPublisher = eventPublisher;
    }

    /// <inheritdoc/>
    public async Task<ReviewKybResponseDto> Handle(ReviewKybCommand request, CancellationToken cancellationToken)
    {
        // Self-approval check: User cannot approve an organization where they hold active membership
        var isMember = await _dbContext.OrganizationMemberships
            .AnyAsync(m => m.OrganizationId == request.OrganizationId && m.UserId == request.AdminUserId && m.Status == MembershipStatus.Active, cancellationToken);

        if (isMember)
        {
            throw new InvalidOperationException("Users cannot approve or reject KYB for an organization they belong to.");
        }

        var org = await _dbContext.Organizations
            .FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Organization with ID {request.OrganizationId} not found.");

        var kybDetail = await _dbContext.KybDetails
            .Where(k => k.OrganizationId == request.OrganizationId)
            .OrderByDescending(k => k.SubmittedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (request.NewStatus == KybStatus.Verified)
        {
            org.SetKybStatus(KybStatus.Verified);
            org.TransitionStatus(OrganizationStatus.Verified);
            kybDetail?.Verify(request.AdminUserId, DateTime.UtcNow);
        }
        else if (request.NewStatus == KybStatus.Rejected)
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
                throw new ArgumentException("Rejection reason is required when rejecting KYB.", nameof(request));

            org.SetKybStatus(KybStatus.Rejected);
            kybDetail?.Reject(request.AdminUserId, request.Reason, DateTime.UtcNow);
        }
        else
        {
            org.SetKybStatus(request.NewStatus);
        }

        // Add audit log entry
        var action = request.NewStatus == KybStatus.Verified ? "Kyb.Verify" : "Kyb.Reject";
        _dbContext.AuditLogs.Add(new AuditLog(request.AdminUserId, action, "Organization", org.Id.ToString(), request.Reason));

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _eventPublisher.PublishAsync(
            new OrganizationStatusChangedDomainEvent(
                org.Id, org.Status, org.Status, request.Reason, DateTime.UtcNow),
            cancellationToken);

        return new ReviewKybResponseDto(org.Id, org.KybStatus.ToString(), org.Status.ToString(), request.Reason);
    }
}
