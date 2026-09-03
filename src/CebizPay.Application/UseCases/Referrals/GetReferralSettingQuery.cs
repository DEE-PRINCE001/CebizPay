using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Referrals.Entities;
using MediatR;

namespace CebizPay.Application.UseCases.Referrals;

/// <summary>
/// Query for administrative retrieval of active global referral parameters.
/// Authorized for SuperAdmin and Auditor roles.
/// </summary>
public sealed record GetReferralSettingQuery : IRequest<ReferralSettingDto>;

/// <summary>
/// Handler for GetReferralSettingQuery.
/// </summary>
public sealed class GetReferralSettingQueryHandler : IRequestHandler<GetReferralSettingQuery, ReferralSettingDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetReferralSettingQueryHandler"/>.
    /// </summary>
    public GetReferralSettingQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<ReferralSettingDto> Handle(GetReferralSettingQuery request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var adminProfile = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == callerUserId && !a.IsDeleted && a.IsActive, cancellationToken);

        if (adminProfile == null || (adminProfile.Role != AdminRoleType.SuperAdmin && adminProfile.Role != AdminRoleType.Auditor))
        {
            throw new UnauthorizedAccessException("Only Super Admin and Auditor may view referral configuration.");
        }

        var setting = await _dbContext.ReferralSettings
            .FirstOrDefaultAsync(s => s.IsActive, cancellationToken);

        if (setting == null)
        {
            setting = ReferralSetting.CreateDefault();
            _dbContext.ReferralSettings.Add(setting);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new ReferralSettingDto(
            RewardAmountPerSuccessfulReferral: setting.RewardAmountPerSuccessfulReferral,
            MaximumSuccessfulReferralsPerUser: setting.MaximumSuccessfulReferralsPerUser,
            IsActive: setting.IsActive,
            Version: setting.Version,
            UpdatedAtUtc: setting.UpdatedAtUtc,
            UpdatedBy: setting.UpdatedBy);
    }
}
