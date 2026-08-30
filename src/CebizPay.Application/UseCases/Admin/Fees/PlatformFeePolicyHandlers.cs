using System.Text.Json;
using CebizPay.Application.Common.Exceptions;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Finance.Entities;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Fees;

/// <summary>
/// Handles <see cref="CreatePlatformFeePolicyCommand"/>.
/// Creates and activates a new platform fee policy version, automatically deactivating the prior active policy.
/// Enforces Super Admin authorization. Audit-logs every mutation.
/// </summary>
public sealed class CreatePlatformFeePolicyCommandHandler : IRequestHandler<CreatePlatformFeePolicyCommand, PlatformFeePolicyResponseDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IPlatformFeePolicyService _feePolicyService;

    /// <summary>
    /// Initializes a new instance of <see cref="CreatePlatformFeePolicyCommandHandler"/>.
    /// </summary>
    public CreatePlatformFeePolicyCommandHandler(
        IApplicationDbContext dbContext,
        IPlatformFeePolicyService feePolicyService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _feePolicyService = feePolicyService ?? throw new ArgumentNullException(nameof(feePolicyService));
    }

    /// <inheritdoc/>
    public async Task<PlatformFeePolicyResponseDto> Handle(CreatePlatformFeePolicyCommand request, CancellationToken cancellationToken)
    {
        // Authorization: Super Admin only
        var adminProfile = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == request.CreatedByUserId && a.IsActive, cancellationToken)
            ?? throw new TransferNotAuthorizedException("Admin profile not found or inactive.");

        if (!adminProfile.HasPermission(Domain.Permissions.Permissions.FeesManagePlatformPolicy))
        {
            throw new TransferNotAuthorizedException(
                "Only Super Admin users with Fees.ManagePlatformPolicy permission can manage platform fee policies.");
        }

        var effectiveFrom = request.EffectiveFromUtc ?? DateTime.UtcNow;

        // Create and activate policy
        var policy = await _feePolicyService.CreateAndActivatePolicyAsync(
            operationType: request.OperationType,
            calculationMethod: request.CalculationMethod,
            feeBearer: request.FeeBearer,
            fixedAmount: request.FixedAmount,
            percentageRate: request.PercentageRate,
            minimumFee: request.MinimumFee,
            maximumFee: request.MaximumFee,
            currency: request.Currency,
            createdByUserId: request.CreatedByUserId,
            effectiveFromUtc: effectiveFrom,
            cancellationToken: cancellationToken);

        // Record audit
        var auditLog = AuditLog.Create(
            actorId: request.CreatedByUserId,
            action: AuditActions.PlatformFeePolicyCreated,
            resourceType: AuditResourceTypes.PlatformFeePolicy,
            resourceId: policy.Id.ToString(),
            afterJson: JsonSerializer.Serialize(new
            {
                OperationType = policy.OperationType.ToString(),
                CalculationMethod = policy.CalculationMethod.ToString(),
                FeeBearer = policy.FeeBearer.ToString(),
                policy.FixedAmount,
                policy.PercentageRate,
                policy.MinimumFee,
                policy.MaximumFee,
                Currency = policy.Currency.ToString(),
                policy.Version,
                policy.EffectiveFromUtc
            }));

        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(policy);
    }

    internal static PlatformFeePolicyResponseDto ToDto(PlatformFeePolicy policy) =>
        new(
            Id: policy.Id,
            OperationType: policy.OperationType.ToString(),
            CalculationMethod: policy.CalculationMethod.ToString(),
            FeeBearer: policy.FeeBearer.ToString(),
            FixedAmount: policy.FixedAmount,
            PercentageRate: policy.PercentageRate,
            MinimumFee: policy.MinimumFee,
            MaximumFee: policy.MaximumFee,
            Currency: policy.Currency.ToString(),
            IsEnabled: policy.IsEnabled,
            Version: policy.Version,
            CreatedByUserId: policy.CreatedByUserId,
            EffectiveFromUtc: policy.EffectiveFromUtc,
            CreatedAtUtc: policy.CreatedAtUtc,
            DeactivatedAtUtc: policy.DeactivatedAtUtc);
}

/// <summary>
/// Handles <see cref="GetAllPlatformFeePoliciesQuery"/>.
/// Returns all historical and current platform fee policies.
/// </summary>
public sealed class GetAllPlatformFeePoliciesQueryHandler : IRequestHandler<GetAllPlatformFeePoliciesQuery, IReadOnlyList<PlatformFeePolicyResponseDto>>
{
    private readonly IPlatformFeePolicyService _feePolicyService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetAllPlatformFeePoliciesQueryHandler"/>.
    /// </summary>
    public GetAllPlatformFeePoliciesQueryHandler(IPlatformFeePolicyService feePolicyService)
    {
        _feePolicyService = feePolicyService ?? throw new ArgumentNullException(nameof(feePolicyService));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PlatformFeePolicyResponseDto>> Handle(GetAllPlatformFeePoliciesQuery request, CancellationToken cancellationToken)
    {
        var policies = await _feePolicyService.GetAllPoliciesAsync(request.OperationType, cancellationToken);
        return policies.Select(CreatePlatformFeePolicyCommandHandler.ToDto).ToList();
    }
}

/// <summary>
/// Handles <see cref="GetActivePlatformFeePolicyQuery"/>.
/// Returns the currently active platform fee policy for the specified operation type, or null if none active.
/// </summary>
public sealed class GetActivePlatformFeePolicyQueryHandler : IRequestHandler<GetActivePlatformFeePolicyQuery, PlatformFeePolicyResponseDto?>
{
    private readonly IPlatformFeePolicyService _feePolicyService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetActivePlatformFeePolicyQueryHandler"/>.
    /// </summary>
    public GetActivePlatformFeePolicyQueryHandler(IPlatformFeePolicyService feePolicyService)
    {
        _feePolicyService = feePolicyService ?? throw new ArgumentNullException(nameof(feePolicyService));
    }

    /// <inheritdoc/>
    public async Task<PlatformFeePolicyResponseDto?> Handle(GetActivePlatformFeePolicyQuery request, CancellationToken cancellationToken)
    {
        var policy = await _feePolicyService.GetActivePolicyAsync(request.OperationType, cancellationToken);
        return policy == null ? null : CreatePlatformFeePolicyCommandHandler.ToDto(policy);
    }
}
