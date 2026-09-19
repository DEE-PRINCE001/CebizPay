using System.Text.Json;
using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Kyb;

/// <summary>
/// Command to query corporate details and directors from official CAC registry.
/// </summary>
public sealed record LookupCacCommand(
    Guid OrganizationId,
    string CacNumber,
    string? CompanyName = null) : IRequest<CacLookupResultDto>;

/// <summary>
/// Result DTO containing verified corporate registry details and director roster.
/// </summary>
public sealed record CacLookupResultDto(
    Guid OrganizationId,
    string CacNumber,
    string CompanyName,
    string? CompanyType,
    string? RegistrationDate,
    string? Status,
    string? Address,
    IReadOnlyList<CacDirectorDto> Directors,
    bool IsMatched,
    string? VerificationReference);

/// <summary>
/// DTO representing an individual corporate director with signatory KYC cross-reference flag.
/// </summary>
public sealed record CacDirectorDto(
    string Name,
    string? Designation,
    bool IsKycVerifiedSignatory);

/// <summary>
/// Validator for LookupCacCommand.
/// </summary>
public sealed class LookupCacCommandValidator : AbstractValidator<LookupCacCommand>
{
    /// <summary>
    /// Initializes validation rules for LookupCacCommand.
    /// </summary>
    public LookupCacCommandValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty()
            .WithMessage("OrganizationId is required.");

        RuleFor(x => x.CacNumber)
            .NotEmpty()
            .MaximumLength(32)
            .WithMessage("Valid CAC registration number is required.");
    }
}

/// <summary>
/// Handler for LookupCacCommand.
/// </summary>
public sealed class LookupCacCommandHandler : IRequestHandler<LookupCacCommand, CacLookupResultDto>
{
    private readonly IVerificationOrchestrator _orchestrator;
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrentOrganizationContext? _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="LookupCacCommandHandler"/>.
    /// </summary>
    public LookupCacCommandHandler(
        IVerificationOrchestrator orchestrator,
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ICurrentOrganizationContext? orgContext = null)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _orgContext = orgContext;
    }

    /// <inheritdoc/>
    public async Task<CacLookupResultDto> Handle(LookupCacCommand request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
            throw new UnauthorizedAccessException("Authentication is required to query corporate registry.");

        // Enforce Tenant Access: verify caller has active membership in target organization
        var membership = await _dbContext.OrganizationMemberships
            .FirstOrDefaultAsync(m => m.OrganizationId == request.OrganizationId && m.UserId == callerUserId && m.Status == MembershipStatus.Active, cancellationToken);

        if (membership == null)
            throw new UnauthorizedAccessException($"Access to organization {request.OrganizationId} is denied.");

        var cleanCacNumber = request.CacNumber.Trim();
        var companyName = request.CompanyName?.Trim() ?? string.Empty;

        var verificationResponse = await _orchestrator.VerifyBusinessAsync(
            request.OrganizationId,
            cleanCacNumber,
            companyName,
            cancellationToken: cancellationToken);

        var isMatched = verificationResponse.LatestResultStatus == VerificationResultStatus.Match ||
                        verificationResponse.Status == VerificationStatus.Completed;

        // Retrieve caller profile to cross-reference signatory identity
        var callerProfile = await _dbContext.IndividualProfiles
            .FirstOrDefaultAsync(p => p.UserId == callerUserId, cancellationToken);

        string? resolvedCompanyName = null;
        string? resolvedCompanyType = null;
        string? resolvedRegDate = null;
        string? resolvedStatus = null;
        string? resolvedAddress = null;
        var directorsList = new List<CacDirectorDto>();

        // Parse evidence SafeMetadata if present
        var evidence = verificationResponse.Evidences.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.SafeMetadata));
        if (evidence?.SafeMetadata != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(evidence.SafeMetadata);
                var root = doc.RootElement;

                if (root.TryGetProperty("company_name", out var cnProp))
                    resolvedCompanyName = cnProp.GetString();
                if (root.TryGetProperty("company_type", out var ctProp))
                    resolvedCompanyType = ctProp.GetString();
                if (root.TryGetProperty("registration_date", out var rdProp))
                    resolvedRegDate = rdProp.GetString();
                if (root.TryGetProperty("status", out var stProp))
                    resolvedStatus = stProp.GetString();
                if (root.TryGetProperty("address", out var addrProp))
                    resolvedAddress = addrProp.GetString();

                if (root.TryGetProperty("directors", out var dirArray) && dirArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var dirElem in dirArray.EnumerateArray())
                    {
                        var dirName = dirElem.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? string.Empty : string.Empty;
                        var designation = dirElem.TryGetProperty("designation", out var desProp) ? desProp.GetString() : "Director";

                        var isVerifiedSignatory = IsCallerMatchingDirector(callerProfile, dirName);
                        directorsList.Add(new CacDirectorDto(dirName, designation, isVerifiedSignatory));
                    }
                }
            }
            catch (JsonException)
            {
                // Fallback to default parsing
            }
        }

        if (isMatched)
        {
            var org = await _dbContext.Organizations
                .FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken);
            if (org != null)
            {
                org.SetCacNumber(cleanCacNumber);
                if (!string.IsNullOrWhiteSpace(resolvedAddress) && string.IsNullOrWhiteSpace(org.Address))
                {
                    org.UpdateProfileDetails(org.Category, resolvedAddress, org.PhotoUrl);
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return new CacLookupResultDto(
            OrganizationId: request.OrganizationId,
            CacNumber: cleanCacNumber,
            CompanyName: resolvedCompanyName ?? (string.IsNullOrWhiteSpace(companyName) ? cleanCacNumber : companyName),
            CompanyType: resolvedCompanyType,
            RegistrationDate: resolvedRegDate,
            Status: resolvedStatus ?? (isMatched ? "ACTIVE" : "UNKNOWN"),
            Address: resolvedAddress,
            Directors: directorsList,
            IsMatched: isMatched,
            VerificationReference: verificationResponse.Reference);
    }

    private static bool IsCallerMatchingDirector(IndividualProfile? profile, string directorName)
    {
        if (profile == null || profile.KycStatus != KycStatus.Verified || string.IsNullOrWhiteSpace(directorName))
            return false;

        var normalizedDirector = directorName.Trim().ToUpperInvariant();
        var normalizedFirst = profile.FirstName.Trim().ToUpperInvariant();
        var normalizedLast = profile.LastName.Trim().ToUpperInvariant();

        return normalizedDirector.Contains(normalizedFirst, StringComparison.Ordinal) &&
               normalizedDirector.Contains(normalizedLast, StringComparison.Ordinal);
    }
}
