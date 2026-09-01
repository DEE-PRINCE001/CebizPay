#pragma warning disable CS1591
using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Compliance.Entities;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Compliance;

/// <summary>
/// Query to retrieve a verification operation and its evidence by canonical reference (e.g. CBZKYC-...).
/// </summary>
public sealed record GetVerificationOperationByReferenceQuery(string Reference) : IRequest<VerificationOperationResponse?>;

public sealed class GetVerificationOperationByReferenceQueryHandler : IRequestHandler<GetVerificationOperationByReferenceQuery, VerificationOperationResponse?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetVerificationOperationByReferenceQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<VerificationOperationResponse?> Handle(GetVerificationOperationByReferenceQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reference))
            return null;

        var op = await _dbContext.VerificationOperations
            .FirstOrDefaultAsync(o => o.Reference == request.Reference.Trim(), cancellationToken);

        if (op == null)
            return null;

        // Verify authorization: check user context or admin profile
        var currentUserId = _currentUserService.UserId;
        var adminProfile = !string.IsNullOrWhiteSpace(currentUserId)
            ? await _dbContext.AdminProfiles.FirstOrDefaultAsync(a => a.UserId == currentUserId && a.IsActive, cancellationToken)
            : null;

        var isAdmin = adminProfile != null && (adminProfile.Role == AdminRoleType.SuperAdmin || adminProfile.Role == AdminRoleType.Auditor);

        if (!isAdmin && op.UserId != null && op.UserId != currentUserId)
            throw new UnauthorizedAccessException("Access denied to verification operation evidence.");

        // Query evidences for this operation
        var evidences = await _dbContext.VerificationEvidences
            .Where(e => e.VerificationOperationId == op.Id)
            .OrderByDescending(e => e.VerifiedAtUtc)
            .ToListAsync(cancellationToken);

        var latestEvidence = evidences.FirstOrDefault();

        return new VerificationOperationResponse(
            op.Id,
            op.Reference,
            op.VerificationType,
            op.Capability,
            op.Status,
            op.PrimaryProvider,
            op.ActiveProvider,
            op.UsedFallback,
            latestEvidence?.ResultStatus,
            latestEvidence?.ConfidenceScore,
            latestEvidence?.SafeMetadata != null ? "Evidence captured." : null,
            op.FailureReason,
            op.CreatedAtUtc,
            op.CompletedAtUtc,
            evidences.Select(e => new VerificationEvidenceSummaryDto(
                e.Id,
                e.Capability,
                e.Provider,
                e.ResultStatus,
                e.ConfidenceScore,
                e.VerifiedAtUtc,
                e.ExpiresAtUtc,
                e.FailureCode,
                e.FailureReason,
                e.SafeMetadata)).ToList());
    }
}

/// <summary>
/// Query to list verification evidence records filtered by user or organization.
/// </summary>
public sealed record GetVerificationEvidenceQuery(
    string? UserId = null,
    Guid? OrganizationId = null,
    VerificationCapability? Capability = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<VerificationEvidenceSummaryDto>>;

public sealed class GetVerificationEvidenceQueryHandler : IRequestHandler<GetVerificationEvidenceQuery, PagedResult<VerificationEvidenceSummaryDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetVerificationEvidenceQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<VerificationEvidenceSummaryDto>> Handle(GetVerificationEvidenceQuery request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        var adminProfile = !string.IsNullOrWhiteSpace(currentUserId)
            ? await _dbContext.AdminProfiles.FirstOrDefaultAsync(a => a.UserId == currentUserId && a.IsActive, cancellationToken)
            : null;

        var isAdmin = adminProfile != null && (adminProfile.Role == AdminRoleType.SuperAdmin || adminProfile.Role == AdminRoleType.Auditor);

        var targetUserId = request.UserId;
        if (!isAdmin)
        {
            // Non-admin can only query their own user evidence
            targetUserId = currentUserId;
        }

        var query = _dbContext.VerificationEvidences.AsQueryable();

        if (!string.IsNullOrWhiteSpace(targetUserId))
            query = query.Where(e => e.UserId == targetUserId);

        if (request.OrganizationId.HasValue && request.OrganizationId.Value != Guid.Empty)
            query = query.Where(e => e.OrganizationId == request.OrganizationId.Value);

        if (request.Capability.HasValue)
            query = query.Where(e => e.Capability == request.Capability.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(1, request.PageNumber);
        var size = Math.Clamp(request.PageSize, 1, 100);

        var evidences = await query
            .OrderByDescending(e => e.VerifiedAtUtc)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        var items = evidences.Select(e => new VerificationEvidenceSummaryDto(
            e.Id,
            e.Capability,
            e.Provider,
            e.ResultStatus,
            e.ConfidenceScore,
            e.VerifiedAtUtc,
            e.ExpiresAtUtc,
            e.FailureCode,
            e.FailureReason,
            e.SafeMetadata)).ToList();

        return new PagedResult<VerificationEvidenceSummaryDto>(items, totalCount, page, size);
    }
}
