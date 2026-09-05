#pragma warning disable CS1591
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Enums;
using FluentValidation;
using MediatR;
using CebizPay.Application.Common.Extensions;

namespace CebizPay.Application.UseCases.Compliance;

/// <summary>
/// Command to initiate individual BVN identity verification.
/// </summary>
public sealed record VerifyBvnCommand(
    string Bvn,
    string FirstName,
    string LastName,
    DateTime? DateOfBirth = null,
    string? IdempotencyKey = null,
    string? TargetUserId = null) : IRequest<VerificationOperationResponse>;

public sealed class VerifyBvnCommandValidator : AbstractValidator<VerifyBvnCommand>
{
    public VerifyBvnCommandValidator()
    {
        RuleFor(x => x.Bvn)
            .NotEmpty().WithMessage("BVN is required.")
            .Matches(@"^\d{11}$").WithMessage("BVN must be an 11-digit numeric value.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100);
    }
}

internal static class ComplianceSecurityHelper
{
    public static async Task VerifyTargetUserAccessAsync(
        string? targetUserId,
        ICurrentUserService currentUserService,
        IApplicationDbContext? dbContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(targetUserId) || targetUserId == currentUserService.UserId)
            return;

        var callerId = currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerId))
            throw new UnauthorizedAccessException("Caller must be authenticated.");

        if (dbContext != null)
        {
            var admin = await dbContext.AdminProfiles
                .FirstOrDefaultAsync(a => a.UserId == callerId && !a.IsDeleted && a.IsActive, cancellationToken);
            if (admin == null || (admin.Role != Domain.Enums.AdminRoleType.SuperAdmin && !admin.HasPermission(Domain.Permissions.Permissions.KycReview) && !admin.HasPermission(Domain.Permissions.Permissions.ComplianceReview)))
            {
                throw new UnauthorizedAccessException("Caller is not authorized to submit compliance verification on behalf of another user.");
            }
        }
    }

    public static async Task VerifyOrganizationAccessAsync(
        Guid organizationId,
        ICurrentUserService? currentUserService,
        IApplicationDbContext? dbContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentUserService?.UserId) || dbContext == null)
            return;

        var callerId = currentUserService.UserId;
        var isMember = await dbContext.OrganizationMemberships
            .AnyAsync(m => m.OrganizationId == organizationId && m.UserId == callerId && m.Status == Domain.Enums.MembershipStatus.Active, cancellationToken);
        if (!isMember)
        {
            var isAdmin = await dbContext.AdminProfiles
                .AnyAsync(a => a.UserId == callerId && !a.IsDeleted && a.IsActive, cancellationToken);
            if (!isAdmin)
            {
                throw new UnauthorizedAccessException("Caller is not authorized to perform compliance operations for this organization.");
            }
        }
    }

    public static async Task VerifyComplianceReadAccessAsync(
        RiskSubjectType subjectType,
        string subjectId,
        Guid? organizationId,
        ICurrentUserService currentUserService,
        IApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var callerId = currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerId))
        {
            throw new UnauthorizedAccessException("Caller must be authenticated.");
        }

        // 1. Platform / Compliance Administrator Access
        var admin = await dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == callerId && !a.IsDeleted && a.IsActive, cancellationToken);
        if (admin != null && (admin.Role == Domain.Enums.AdminRoleType.SuperAdmin ||
                              admin.Role == Domain.Enums.AdminRoleType.Admin ||
                              admin.HasPermission(Domain.Permissions.Permissions.ComplianceView) ||
                              admin.HasPermission(Domain.Permissions.Permissions.ComplianceReview) ||
                              admin.HasPermission(Domain.Permissions.Permissions.KycView) ||
                              admin.HasPermission(Domain.Permissions.Permissions.KybView)))
        {
            return;
        }

        // 2. Individual Self-Service Access
        if (subjectType == RiskSubjectType.Individual)
        {
            if (string.Equals(callerId, subjectId, StringComparison.Ordinal))
            {
                return;
            }

            throw new UnauthorizedAccessException("Caller is not authorized to view compliance data for this subject.");
        }

        // 3. Organization-Scoped Access
        if (subjectType == RiskSubjectType.Organization || organizationId.HasValue)
        {
            var targetOrgId = organizationId ?? (Guid.TryParse(subjectId, out var parsedOrgId) ? parsedOrgId : Guid.Empty);
            if (targetOrgId != Guid.Empty)
            {
                var membership = await dbContext.OrganizationMemberships
                    .FirstOrDefaultAsync(m => m.OrganizationId == targetOrgId && m.UserId == callerId && m.Status == Domain.Enums.MembershipStatus.Active, cancellationToken);

                if (membership != null && (membership.Role == Domain.Enums.MembershipRoleType.Owner ||
                                           membership.Role == Domain.Enums.MembershipRoleType.Admin ||
                                           membership.HasPermission(Domain.Permissions.Permissions.KybView) ||
                                           membership.HasPermission(Domain.Permissions.Permissions.ComplianceView)))
                {
                    return;
                }
            }

            throw new UnauthorizedAccessException("Caller is not authorized to view compliance data for this organization.");
        }

        throw new UnauthorizedAccessException("Caller is not authorized to view compliance data.");
    }
}

public sealed class VerifyBvnCommandHandler : IRequestHandler<VerifyBvnCommand, VerificationOperationResponse>
{
    private readonly IVerificationOrchestrator _orchestrator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext? _dbContext;

    public VerifyBvnCommandHandler(
        IVerificationOrchestrator orchestrator,
        ICurrentUserService currentUserService,
        IApplicationDbContext? dbContext = null)
    {
        _orchestrator = orchestrator;
        _currentUserService = currentUserService;
        _dbContext = dbContext;
    }

    public async Task<VerificationOperationResponse> Handle(VerifyBvnCommand request, CancellationToken cancellationToken)
    {
        await ComplianceSecurityHelper.VerifyTargetUserAccessAsync(request.TargetUserId, _currentUserService, _dbContext, cancellationToken);

        var effectiveUserId = !string.IsNullOrWhiteSpace(request.TargetUserId) ? request.TargetUserId : _currentUserService.UserId;

        if (string.IsNullOrWhiteSpace(effectiveUserId))
            throw new UnauthorizedAccessException("User must be authenticated to perform KYC identity verification.");

        return await _orchestrator.VerifyBvnAsync(
            effectiveUserId,
            request.Bvn,
            request.FirstName,
            request.LastName,
            request.DateOfBirth,
            request.IdempotencyKey,
            cancellationToken);
    }
}

/// <summary>
/// Command to initiate individual NIN identity verification.
/// </summary>
public sealed record VerifyNinCommand(
    string Nin,
    string FirstName,
    string LastName,
    DateTime? DateOfBirth = null,
    string? IdempotencyKey = null,
    string? TargetUserId = null) : IRequest<VerificationOperationResponse>;

public sealed class VerifyNinCommandValidator : AbstractValidator<VerifyNinCommand>
{
    public VerifyNinCommandValidator()
    {
        RuleFor(x => x.Nin)
            .NotEmpty().WithMessage("NIN is required.")
            .Matches(@"^\d{11}$").WithMessage("NIN must be an 11-digit numeric value.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100);
    }
}

public sealed class VerifyNinCommandHandler : IRequestHandler<VerifyNinCommand, VerificationOperationResponse>
{
    private readonly IVerificationOrchestrator _orchestrator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext? _dbContext;

    public VerifyNinCommandHandler(
        IVerificationOrchestrator orchestrator,
        ICurrentUserService currentUserService,
        IApplicationDbContext? dbContext = null)
    {
        _orchestrator = orchestrator;
        _currentUserService = currentUserService;
        _dbContext = dbContext;
    }

    public async Task<VerificationOperationResponse> Handle(VerifyNinCommand request, CancellationToken cancellationToken)
    {
        await ComplianceSecurityHelper.VerifyTargetUserAccessAsync(request.TargetUserId, _currentUserService, _dbContext, cancellationToken);

        var effectiveUserId = !string.IsNullOrWhiteSpace(request.TargetUserId) ? request.TargetUserId : _currentUserService.UserId;

        if (string.IsNullOrWhiteSpace(effectiveUserId))
            throw new UnauthorizedAccessException("User must be authenticated to perform KYC identity verification.");

        return await _orchestrator.VerifyNinAsync(
            effectiveUserId,
            request.Nin,
            request.FirstName,
            request.LastName,
            request.DateOfBirth,
            request.IdempotencyKey,
            cancellationToken);
    }
}

/// <summary>
/// Command to perform biometric liveness check and 1:1 facial biometric matching.
/// </summary>
public sealed record VerifyBiometricsCommand(
    string SelfieImageBase64,
    string? ReferenceImageBase64 = null,
    string? IdNumber = null,
    string? IdempotencyKey = null,
    string? TargetUserId = null) : IRequest<VerificationOperationResponse>;

public sealed class VerifyBiometricsCommandValidator : AbstractValidator<VerifyBiometricsCommand>
{
    public VerifyBiometricsCommandValidator()
    {
        RuleFor(x => x.SelfieImageBase64)
            .NotEmpty().WithMessage("Selfie image data is required.");
    }
}

public sealed class VerifyBiometricsCommandHandler : IRequestHandler<VerifyBiometricsCommand, VerificationOperationResponse>
{
    private readonly IVerificationOrchestrator _orchestrator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext? _dbContext;

    public VerifyBiometricsCommandHandler(
        IVerificationOrchestrator orchestrator,
        ICurrentUserService currentUserService,
        IApplicationDbContext? dbContext = null)
    {
        _orchestrator = orchestrator;
        _currentUserService = currentUserService;
        _dbContext = dbContext;
    }

    public async Task<VerificationOperationResponse> Handle(VerifyBiometricsCommand request, CancellationToken cancellationToken)
    {
        await ComplianceSecurityHelper.VerifyTargetUserAccessAsync(request.TargetUserId, _currentUserService, _dbContext, cancellationToken);

        var effectiveUserId = !string.IsNullOrWhiteSpace(request.TargetUserId) ? request.TargetUserId : _currentUserService.UserId;

        if (string.IsNullOrWhiteSpace(effectiveUserId))
            throw new UnauthorizedAccessException("User must be authenticated to perform biometric verification.");

        return await _orchestrator.VerifyBiometricsAsync(
            effectiveUserId,
            request.SelfieImageBase64,
            request.ReferenceImageBase64,
            request.IdNumber,
            request.IdempotencyKey,
            cancellationToken);
    }
}

/// <summary>
/// Command to verify a government-issued identity document.
/// </summary>
public sealed record VerifyDocumentCommand(
    DocumentType DocumentType,
    string DocumentNumber,
    string DocumentImageBase64,
    string? FirstName = null,
    string? LastName = null,
    string? IdempotencyKey = null,
    string? TargetUserId = null) : IRequest<VerificationOperationResponse>;

public sealed class VerifyDocumentCommandValidator : AbstractValidator<VerifyDocumentCommand>
{
    public VerifyDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentNumber)
            .NotEmpty().WithMessage("Document number is required.")
            .MaximumLength(50);

        RuleFor(x => x.DocumentImageBase64)
            .NotEmpty().WithMessage("Document image data is required.");
    }
}

public sealed class VerifyDocumentCommandHandler : IRequestHandler<VerifyDocumentCommand, VerificationOperationResponse>
{
    private readonly IVerificationOrchestrator _orchestrator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext? _dbContext;

    public VerifyDocumentCommandHandler(
        IVerificationOrchestrator orchestrator,
        ICurrentUserService currentUserService,
        IApplicationDbContext? dbContext = null)
    {
        _orchestrator = orchestrator;
        _currentUserService = currentUserService;
        _dbContext = dbContext;
    }

    public async Task<VerificationOperationResponse> Handle(VerifyDocumentCommand request, CancellationToken cancellationToken)
    {
        await ComplianceSecurityHelper.VerifyTargetUserAccessAsync(request.TargetUserId, _currentUserService, _dbContext, cancellationToken);

        var effectiveUserId = !string.IsNullOrWhiteSpace(request.TargetUserId) ? request.TargetUserId : _currentUserService.UserId;

        if (string.IsNullOrWhiteSpace(effectiveUserId))
            throw new UnauthorizedAccessException("User must be authenticated to perform document verification.");

        return await _orchestrator.VerifyDocumentAsync(
            effectiveUserId,
            request.DocumentType,
            request.DocumentNumber,
            request.DocumentImageBase64,
            request.FirstName,
            request.LastName,
            request.IdempotencyKey,
            cancellationToken);
    }
}

/// <summary>
/// Command to screen an individual or entity against AML / PEP / Sanctions watchlists.
/// </summary>
public sealed record ScreenAmlCommand(
    string Name,
    bool IsEntity = false,
    Guid? OrganizationId = null,
    string? RegistrationNumber = null,
    DateTime? DateOfBirth = null,
    string? CountryCode = "NG",
    string? IdempotencyKey = null,
    string? TargetUserId = null) : IRequest<VerificationOperationResponse>;

public sealed class ScreenAmlCommandValidator : AbstractValidator<ScreenAmlCommand>
{
    public ScreenAmlCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200);
    }
}

public sealed class ScreenAmlCommandHandler : IRequestHandler<ScreenAmlCommand, VerificationOperationResponse>
{
    private readonly IVerificationOrchestrator _orchestrator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext? _dbContext;

    public ScreenAmlCommandHandler(
        IVerificationOrchestrator orchestrator,
        ICurrentUserService currentUserService,
        IApplicationDbContext? dbContext = null)
    {
        _orchestrator = orchestrator;
        _currentUserService = currentUserService;
        _dbContext = dbContext;
    }

    public async Task<VerificationOperationResponse> Handle(ScreenAmlCommand request, CancellationToken cancellationToken)
    {
        if (request.IsEntity)
        {
            if (!request.OrganizationId.HasValue || request.OrganizationId.Value == Guid.Empty)
                throw new ArgumentException("OrganizationId is required for entity AML screening.", nameof(request));

            await ComplianceSecurityHelper.VerifyOrganizationAccessAsync(request.OrganizationId.Value, _currentUserService, _dbContext, cancellationToken);

            return await _orchestrator.ScreenEntityAmlAsync(
                request.OrganizationId.Value,
                request.Name,
                request.RegistrationNumber,
                request.CountryCode,
                request.IdempotencyKey,
                cancellationToken);
        }

        await ComplianceSecurityHelper.VerifyTargetUserAccessAsync(request.TargetUserId, _currentUserService, _dbContext, cancellationToken);

        var effectiveUserId = !string.IsNullOrWhiteSpace(request.TargetUserId) ? request.TargetUserId : _currentUserService.UserId;

        if (string.IsNullOrWhiteSpace(effectiveUserId))
            throw new UnauthorizedAccessException("User must be authenticated to perform AML screening.");

        return await _orchestrator.ScreenIndividualAmlAsync(
            effectiveUserId,
            request.Name,
            request.DateOfBirth,
            request.CountryCode,
            request.IdempotencyKey,
            cancellationToken);
    }
}

/// <summary>
/// Command to verify a corporate legal entity / organization via CAC registry.
/// </summary>
public sealed record VerifyBusinessCommand(
    Guid OrganizationId,
    string CacNumber,
    string CompanyName,
    string? IdempotencyKey = null) : IRequest<VerificationOperationResponse>;

public sealed class VerifyBusinessCommandValidator : AbstractValidator<VerifyBusinessCommand>
{
    public VerifyBusinessCommandValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("OrganizationId is required.");

        RuleFor(x => x.CacNumber)
            .NotEmpty().WithMessage("CAC registration number is required.")
            .MaximumLength(50);

        RuleFor(x => x.CompanyName)
            .NotEmpty().WithMessage("Company name is required.")
            .MaximumLength(200);
    }
}

public sealed class VerifyBusinessCommandHandler : IRequestHandler<VerifyBusinessCommand, VerificationOperationResponse>
{
    private readonly IVerificationOrchestrator _orchestrator;
    private readonly ICurrentUserService? _currentUserService;
    private readonly IApplicationDbContext? _dbContext;

    public VerifyBusinessCommandHandler(
        IVerificationOrchestrator orchestrator,
        ICurrentUserService? currentUserService = null,
        IApplicationDbContext? dbContext = null)
    {
        _orchestrator = orchestrator;
        _currentUserService = currentUserService;
        _dbContext = dbContext;
    }

    public async Task<VerificationOperationResponse> Handle(VerifyBusinessCommand request, CancellationToken cancellationToken)
    {
        await ComplianceSecurityHelper.VerifyOrganizationAccessAsync(request.OrganizationId, _currentUserService, _dbContext, cancellationToken);

        return await _orchestrator.VerifyBusinessAsync(
            request.OrganizationId,
            request.CacNumber,
            request.CompanyName,
            request.IdempotencyKey,
            cancellationToken);
    }
}

/// <summary>
/// Command to query beneficial owners and directors for an organization.
/// </summary>
public sealed record GetBeneficialOwnersCommand(
    Guid OrganizationId,
    string CacNumber,
    string? IdempotencyKey = null) : IRequest<VerificationOperationResponse>;

public sealed class GetBeneficialOwnersCommandHandler : IRequestHandler<GetBeneficialOwnersCommand, VerificationOperationResponse>
{
    private readonly IVerificationOrchestrator _orchestrator;
    private readonly ICurrentUserService? _currentUserService;
    private readonly IApplicationDbContext? _dbContext;

    public GetBeneficialOwnersCommandHandler(
        IVerificationOrchestrator orchestrator,
        ICurrentUserService? currentUserService = null,
        IApplicationDbContext? dbContext = null)
    {
        _orchestrator = orchestrator;
        _currentUserService = currentUserService;
        _dbContext = dbContext;
    }

    public async Task<VerificationOperationResponse> Handle(GetBeneficialOwnersCommand request, CancellationToken cancellationToken)
    {
        await ComplianceSecurityHelper.VerifyOrganizationAccessAsync(request.OrganizationId, _currentUserService, _dbContext, cancellationToken);

        return await _orchestrator.GetBeneficialOwnersAsync(
            request.OrganizationId,
            request.CacNumber,
            request.IdempotencyKey,
            cancellationToken);
    }
}
