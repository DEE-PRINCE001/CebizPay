using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Referrals.Entities;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Referrals;

/// <summary>
/// Command to update global referral program settings under Super Admin authorization.
/// </summary>
public sealed record UpdateReferralSettingCommand(
    decimal RewardAmountPerSuccessfulReferral,
    int MaximumSuccessfulReferralsPerUser,
    bool IsActive) : IRequest<ReferralSettingDto>;

/// <summary>
/// Validator for UpdateReferralSettingCommand.
/// </summary>
public sealed class UpdateReferralSettingCommandValidator : AbstractValidator<UpdateReferralSettingCommand>
{
    /// <summary>
    /// Initializes validation rules for UpdateReferralSettingCommand.
    /// </summary>
    public UpdateReferralSettingCommandValidator()
    {
        RuleFor(x => x.RewardAmountPerSuccessfulReferral)
            .GreaterThan(0).WithMessage("Reward amount per successful referral must be strictly positive.")
            .LessThanOrEqualTo(100_000).WithMessage("Reward amount cannot exceed 100,000 NGN per referral.");

        RuleFor(x => x.MaximumSuccessfulReferralsPerUser)
            .GreaterThan(0).WithMessage("Maximum successful referrals per user must be strictly positive.")
            .LessThanOrEqualTo(10_000).WithMessage("Maximum successful referrals cannot exceed 10,000.");
    }
}

/// <summary>
/// Handler for UpdateReferralSettingCommand.
/// </summary>
public sealed class UpdateReferralSettingCommandHandler : IRequestHandler<UpdateReferralSettingCommand, ReferralSettingDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    /// <summary>
    /// Initializes a new instance of <see cref="UpdateReferralSettingCommandHandler"/>.
    /// </summary>
    public UpdateReferralSettingCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
    }

    /// <inheritdoc/>
    public async Task<ReferralSettingDto> Handle(UpdateReferralSettingCommand request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var adminProfile = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == callerUserId && !a.IsDeleted && a.IsActive, cancellationToken);

        if (adminProfile == null || adminProfile.Role != AdminRoleType.SuperAdmin)
        {
            throw new UnauthorizedAccessException("Only active Super Admins can update referral settings.");
        }

        var setting = await _dbContext.ReferralSettings
            .FirstOrDefaultAsync(s => s.IsActive, cancellationToken);

        var now = DateTime.UtcNow;

        if (setting == null)
        {
            setting = ReferralSetting.CreateDefault(
                defaultReward: request.RewardAmountPerSuccessfulReferral,
                defaultMaxReferrals: request.MaximumSuccessfulReferralsPerUser,
                createdBy: callerUserId);
            _dbContext.ReferralSettings.Add(setting);
        }
        else
        {
            setting.Update(
                rewardAmount: request.RewardAmountPerSuccessfulReferral,
                maximumReferrals: request.MaximumSuccessfulReferralsPerUser,
                isActive: request.IsActive,
                updatedBy: callerUserId,
                now: now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Audit setting modification
        await _auditLogService.LogAsync(
            action: AuditActions.ReferralSettingUpdated,
            resourceType: AuditResourceTypes.ReferralSetting,
            resourceId: setting.Id.ToString(),
            details: $"Referral setting updated: Reward={setting.RewardAmountPerSuccessfulReferral:N2} NGN, Max={setting.MaximumSuccessfulReferralsPerUser}, Active={setting.IsActive}",
            cancellationToken: cancellationToken);

        return new ReferralSettingDto(
            RewardAmountPerSuccessfulReferral: setting.RewardAmountPerSuccessfulReferral,
            MaximumSuccessfulReferralsPerUser: setting.MaximumSuccessfulReferralsPerUser,
            IsActive: setting.IsActive,
            Version: setting.Version,
            UpdatedAtUtc: setting.UpdatedAtUtc,
            UpdatedBy: setting.UpdatedBy);
    }
}
