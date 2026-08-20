using CebizPay.Application.Common.Exceptions;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Fees;

/// <summary>
/// Handles <see cref="CreateBankTransferFeePolicyCommand"/>.
/// Creates and activates a new platform bank-transfer fee policy.
/// Enforces Super Admin authorization before mutating the policy.
/// Every policy change is audit-logged.
/// </summary>
public sealed class CreateBankTransferFeePolicyCommandHandler : IRequestHandler<CreateBankTransferFeePolicyCommand, BankTransferFeePolicyResponseDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IBankTransferFeePolicyService _feePolicyService;

    /// <summary>
    /// Initializes a new instance of <see cref="CreateBankTransferFeePolicyCommandHandler"/>.
    /// </summary>
    public CreateBankTransferFeePolicyCommandHandler(IApplicationDbContext dbContext, IBankTransferFeePolicyService feePolicyService)
    {
        _dbContext = dbContext;
        _feePolicyService = feePolicyService;
    }

    /// <inheritdoc/>
    public async Task<BankTransferFeePolicyResponseDto> Handle(CreateBankTransferFeePolicyCommand request, CancellationToken cancellationToken)
    {
        // Authorization: only Super Admin with FeesManageBankTransferPolicy can manage policy
        var adminProfile = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == request.CreatedByUserId && a.IsActive, cancellationToken)
            ?? throw new TransferNotAuthorizedException("Admin profile not found or inactive.");

        if (!adminProfile.HasPermission(Domain.Permissions.Permissions.FeesManageBankTransferPolicy))
            throw new TransferNotAuthorizedException(
                "Only Super Admin users with Fees.ManageBankTransferPolicy permission can manage bank-transfer fee policies.");

        // Create and activate the new policy
        var policy = await _feePolicyService.CreateAndActivatePolicyAsync(
            request.Mode,
            request.PercentageRate,
            request.MinimumFee,
            request.MaximumFee,
            request.CreatedByUserId,
            cancellationToken);

        // Audit log the policy change
        var auditLog = Domain.Entities.AuditLog.Create(
            actorId: request.CreatedByUserId,
            action: Domain.Auditing.AuditActions.BankTransferFeePolicyCreated,
            resourceType: Domain.Auditing.AuditResourceTypes.BankTransferFeePolicy,
            resourceId: policy.Id.ToString(),
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                Version = policy.Version,
                Mode = policy.Mode.ToString(),
                policy.PercentageRate,
                policy.MinimumFee,
                policy.MaximumFee,
                policy.EffectiveFrom
            }));

        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(policy);
    }

    internal static BankTransferFeePolicyResponseDto ToDto(Domain.Finance.Entities.BankTransferFeePolicy policy) =>
        new(policy.Id, policy.Mode.ToString(), policy.PercentageRate, policy.MinimumFee, policy.MaximumFee,
            policy.IsEnabled, policy.Version, policy.CreatedByUserId, policy.EffectiveFrom,
            policy.CreatedAtUtc, policy.DeactivatedAtUtc);
}

/// <summary>
/// Handles <see cref="GetAllBankTransferFeePoliciesQuery"/>.
/// Returns all historical and current bank-transfer fee policies.
/// </summary>
public sealed class GetAllBankTransferFeePoliciesQueryHandler : IRequestHandler<GetAllBankTransferFeePoliciesQuery, IReadOnlyList<BankTransferFeePolicyResponseDto>>
{
    private readonly IBankTransferFeePolicyService _feePolicyService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetAllBankTransferFeePoliciesQueryHandler"/>.
    /// </summary>
    public GetAllBankTransferFeePoliciesQueryHandler(IBankTransferFeePolicyService feePolicyService)
    {
        _feePolicyService = feePolicyService;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BankTransferFeePolicyResponseDto>> Handle(GetAllBankTransferFeePoliciesQuery request, CancellationToken cancellationToken)
    {
        var policies = await _feePolicyService.GetAllPoliciesAsync(cancellationToken);
        return policies.Select(CreateBankTransferFeePolicyCommandHandler.ToDto).ToList();
    }
}

/// <summary>
/// Handles <see cref="GetActiveBankTransferFeePolicyQuery"/>.
/// Returns the currently active bank-transfer fee policy, or null if none is configured.
/// </summary>
public sealed class GetActiveBankTransferFeePolicyQueryHandler : IRequestHandler<GetActiveBankTransferFeePolicyQuery, BankTransferFeePolicyResponseDto?>
{
    private readonly IBankTransferFeePolicyService _feePolicyService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetActiveBankTransferFeePolicyQueryHandler"/>.
    /// </summary>
    public GetActiveBankTransferFeePolicyQueryHandler(IBankTransferFeePolicyService feePolicyService)
    {
        _feePolicyService = feePolicyService;
    }

    /// <inheritdoc/>
    public async Task<BankTransferFeePolicyResponseDto?> Handle(GetActiveBankTransferFeePolicyQuery request, CancellationToken cancellationToken)
    {
        var policy = await _feePolicyService.GetActivePolicyAsync(cancellationToken);
        return policy == null ? null : CreateBankTransferFeePolicyCommandHandler.ToDto(policy);
    }
}
