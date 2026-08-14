using CebizPay.Application.Common.Exceptions;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Application.UseCases.Admin.Fees;

/// <summary>
/// Handles <see cref="CreateFeePolicyCommand"/>. Creates and activates a new peer-transfer fee policy.
/// Enforces Super Admin authorization before mutating the policy.
/// Every policy change is audit-logged.
/// </summary>
public sealed class CreateFeePolicyCommandHandler : IRequestHandler<CreateFeePolicyCommand, FeePolicyResponseDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IFeePolicyService _feePolicyService;

    /// <summary>
    /// Initializes a new instance of <see cref="CreateFeePolicyCommandHandler"/>.
    /// </summary>
    public CreateFeePolicyCommandHandler(IApplicationDbContext dbContext, IFeePolicyService feePolicyService)
    {
        _dbContext = dbContext;
        _feePolicyService = feePolicyService;
    }

    /// <inheritdoc/>
    public async Task<FeePolicyResponseDto> Handle(CreateFeePolicyCommand request, CancellationToken cancellationToken)
    {
        // Authorization: only Super Admin can manage fee policy
        var adminProfile = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == request.CreatedByUserId && a.IsActive, cancellationToken)
            ?? throw new TransferNotAuthorizedException("Admin profile not found or inactive.");

        if (!adminProfile.HasPermission(Domain.Permissions.Permissions.FeesManagePeerTransferPolicy))
            throw new TransferNotAuthorizedException(
                "Only Super Admin users with Fees.ManagePeerTransferPolicy permission can manage fee policies.");

        // Create and activate the new policy (deactivates any existing active policy)
        var policy = await _feePolicyService.CreateAndActivatePolicyAsync(
            request.Mode,
            request.PercentageRate,
            request.MinimumFee,
            request.MaximumFee,
            request.CreatedByUserId,
            cancellationToken);

        // Audit log the policy change
        var auditLog = new AuditLog(
            actorUserId: request.CreatedByUserId,
            action: "Fees.CreatePeerTransferPolicy",
            entityType: "PeerTransferFeePolicy",
            entityId: policy.Id.ToString(),
            detailsJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                Version = policy.Version,
                Mode = policy.Mode.ToString(),
                policy.PercentageRate,
                policy.MinimumFee,
                policy.MaximumFee
            }));

        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(policy);
    }

    internal static FeePolicyResponseDto ToDto(Domain.Finance.Entities.PeerTransferFeePolicy policy) =>
        new(policy.Id, policy.Mode.ToString(), policy.PercentageRate, policy.MinimumFee, policy.MaximumFee,
            policy.IsEnabled, policy.Version, policy.CreatedByUserId, policy.EffectiveFrom,
            policy.CreatedAtUtc, policy.DeactivatedAtUtc);
}

/// <summary>
/// Handles <see cref="GetAllFeePoliciesQuery"/>. Returns all historical and current fee policies.
/// </summary>
public sealed class GetAllFeePoliciesQueryHandler : IRequestHandler<GetAllFeePoliciesQuery, IReadOnlyList<FeePolicyResponseDto>>
{
    private readonly IFeePolicyService _feePolicyService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetAllFeePoliciesQueryHandler"/>.
    /// </summary>
    public GetAllFeePoliciesQueryHandler(IFeePolicyService feePolicyService)
    {
        _feePolicyService = feePolicyService;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FeePolicyResponseDto>> Handle(GetAllFeePoliciesQuery request, CancellationToken cancellationToken)
    {
        var policies = await _feePolicyService.GetAllPoliciesAsync(cancellationToken);
        return policies.Select(CreateFeePolicyCommandHandler.ToDto).ToList();
    }
}

/// <summary>
/// Handles <see cref="GetActiveFeePolicyQuery"/>. Returns the currently active fee policy.
/// </summary>
public sealed class GetActiveFeePolicyQueryHandler : IRequestHandler<GetActiveFeePolicyQuery, FeePolicyResponseDto?>
{
    private readonly IFeePolicyService _feePolicyService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetActiveFeePolicyQueryHandler"/>.
    /// </summary>
    public GetActiveFeePolicyQueryHandler(IFeePolicyService feePolicyService)
    {
        _feePolicyService = feePolicyService;
    }

    /// <inheritdoc/>
    public async Task<FeePolicyResponseDto?> Handle(GetActiveFeePolicyQuery request, CancellationToken cancellationToken)
    {
        var policy = await _feePolicyService.GetActivePolicyAsync(cancellationToken);
        return policy == null ? null : CreateFeePolicyCommandHandler.ToDto(policy);
    }
}
