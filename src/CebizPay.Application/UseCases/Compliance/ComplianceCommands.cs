#pragma warning disable CS1591
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Enums;
using FluentValidation;
using MediatR;

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

public sealed class VerifyBvnCommandHandler : IRequestHandler<VerifyBvnCommand, VerificationOperationResponse>
{
    private readonly IVerificationOrchestrator _orchestrator;
    private readonly ICurrentUserService _currentUserService;

    public VerifyBvnCommandHandler(
        IVerificationOrchestrator orchestrator,
        ICurrentUserService currentUserService)
    {
        _orchestrator = orchestrator;
        _currentUserService = currentUserService;
    }

    public async Task<VerificationOperationResponse> Handle(VerifyBvnCommand request, CancellationToken cancellationToken)
    {
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

    public VerifyNinCommandHandler(
        IVerificationOrchestrator orchestrator,
        ICurrentUserService currentUserService)
    {
        _orchestrator = orchestrator;
        _currentUserService = currentUserService;
    }

    public async Task<VerificationOperationResponse> Handle(VerifyNinCommand request, CancellationToken cancellationToken)
    {
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

    public VerifyBiometricsCommandHandler(
        IVerificationOrchestrator orchestrator,
        ICurrentUserService currentUserService)
    {
        _orchestrator = orchestrator;
        _currentUserService = currentUserService;
    }

    public async Task<VerificationOperationResponse> Handle(VerifyBiometricsCommand request, CancellationToken cancellationToken)
    {
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

    public VerifyDocumentCommandHandler(
        IVerificationOrchestrator orchestrator,
        ICurrentUserService currentUserService)
    {
        _orchestrator = orchestrator;
        _currentUserService = currentUserService;
    }

    public async Task<VerificationOperationResponse> Handle(VerifyDocumentCommand request, CancellationToken cancellationToken)
    {
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

    public ScreenAmlCommandHandler(
        IVerificationOrchestrator orchestrator,
        ICurrentUserService currentUserService)
    {
        _orchestrator = orchestrator;
        _currentUserService = currentUserService;
    }

    public async Task<VerificationOperationResponse> Handle(ScreenAmlCommand request, CancellationToken cancellationToken)
    {
        if (request.IsEntity)
        {
            if (!request.OrganizationId.HasValue || request.OrganizationId.Value == Guid.Empty)
                throw new ArgumentException("OrganizationId is required for entity AML screening.", nameof(request));

            return await _orchestrator.ScreenEntityAmlAsync(
                request.OrganizationId.Value,
                request.Name,
                request.RegistrationNumber,
                request.CountryCode,
                request.IdempotencyKey,
                cancellationToken);
        }

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

    public VerifyBusinessCommandHandler(IVerificationOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task<VerificationOperationResponse> Handle(VerifyBusinessCommand request, CancellationToken cancellationToken)
    {
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

    public GetBeneficialOwnersCommandHandler(IVerificationOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task<VerificationOperationResponse> Handle(GetBeneficialOwnersCommand request, CancellationToken cancellationToken)
    {
        return await _orchestrator.GetBeneficialOwnersAsync(
            request.OrganizationId,
            request.CacNumber,
            request.IdempotencyKey,
            cancellationToken);
    }
}
