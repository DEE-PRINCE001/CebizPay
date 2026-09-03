using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Referrals;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Referrals.Entities;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Referrals;

/// <summary>
/// Command for a newly registered or onboarding user to associate with a referrer via referral code.
/// </summary>
public sealed record ClaimReferralCodeCommand(
    string ReferralCode) : IRequest<Guid>;

/// <summary>
/// Validator for ClaimReferralCodeCommand.
/// </summary>
public sealed class ClaimReferralCodeCommandValidator : AbstractValidator<ClaimReferralCodeCommand>
{
    /// <summary>
    /// Initializes validation rules for ClaimReferralCodeCommand.
    /// </summary>
    public ClaimReferralCodeCommandValidator()
    {
        RuleFor(x => x.ReferralCode)
            .NotEmpty().WithMessage("Referral code is required.")
            .MaximumLength(32).WithMessage("Referral code cannot exceed 32 characters.");
    }
}

/// <summary>
/// Handler for ClaimReferralCodeCommand.
/// </summary>
public sealed class ClaimReferralCodeCommandHandler : IRequestHandler<ClaimReferralCodeCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IReferralQualificationService _qualificationService;
    private readonly IAuditLogService _auditLogService;

    /// <summary>
    /// Initializes a new instance of <see cref="ClaimReferralCodeCommandHandler"/>.
    /// </summary>
    public ClaimReferralCodeCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IReferralQualificationService qualificationService,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _qualificationService = qualificationService ?? throw new ArgumentNullException(nameof(qualificationService));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
    }

    /// <inheritdoc/>
    public async Task<Guid> Handle(ClaimReferralCodeCommand request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var normalizedCode = request.ReferralCode.Trim().ToUpperInvariant();

        // 1. Verify code exists and is active
        var referralCode = await _dbContext.ReferralCodes
            .FirstOrDefaultAsync(c => c.Code == normalizedCode, cancellationToken)
            ?? throw new InvalidOperationException("Invalid or non-existent referral code.");

        if (!referralCode.IsActive)
        {
            throw new InvalidOperationException("Referral code is inactive.");
        }

        // 2. Strict self-referral check
        if (string.Equals(referralCode.UserId, callerUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Self-referral is strictly forbidden.");
        }

        // 3. Prevent multiple referrers for the same referred user
        var alreadyReferred = await _dbContext.ReferralRelationships
            .AnyAsync(r => r.ReferredUserId == callerUserId, cancellationToken);

        if (alreadyReferred)
        {
            throw new InvalidOperationException("User has already claimed a referral code.");
        }

        // 4. Create and persist relationship
        var now = DateTime.UtcNow;
        var relationship = ReferralRelationship.Create(
            referralCode.UserId,
            callerUserId,
            referralCode.Id,
            referralCode.Code,
            now);

        _dbContext.ReferralRelationships.Add(relationship);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 5. Audit relationship claim
        await _auditLogService.LogAsync(
            action: AuditActions.ReferralClaimed,
            resourceType: AuditResourceTypes.ReferralRelationship,
            resourceId: relationship.Id.ToString(),
            details: $"User '{callerUserId}' claimed referral code '{referralCode.Code}' owned by '{referralCode.UserId}'",
            cancellationToken: cancellationToken);

        // 6. Evaluate qualification in case user already satisfied KYC & deposit milestones
        await _qualificationService.EvaluateQualificationAsync(callerUserId, cancellationToken);

        return relationship.Id;
    }
}
