using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Permissions;
using FluentValidation;
using MediatR;
using CebizPay.Application.Common.Extensions;

namespace CebizPay.Application.UseCases.Individuals.SubmitKyc;

/// <summary>
/// Command to submit an individual KYC document (NIMC, Driver's License, International Passport, Liveness).
/// </summary>
public sealed record SubmitKycCommand(
    string UserId,
    DocumentType DocumentType,
    string DocumentNumber,
    string DocumentUrl) : IRequest<SubmitKycResponseDto>;

/// <summary>
/// Response DTO for KYC submission.
/// </summary>
public sealed record SubmitKycResponseDto(
    Guid DocumentId,
    string UserId,
    string DocumentType,
    string DocumentNumber,
    string Status);

/// <summary>
/// Validator for SubmitKycCommand.
/// </summary>
public sealed class SubmitKycCommandValidator : AbstractValidator<SubmitKycCommand>
{
    /// <summary>
    /// Initializes validation rules for SubmitKycCommand.
    /// </summary>
    public SubmitKycCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("UserId is required.");
        RuleFor(x => x.DocumentType).IsInEnum().WithMessage("Valid DocumentType is required.");
        RuleFor(x => x.DocumentNumber).NotEmpty().WithMessage("DocumentNumber is required.");
        RuleFor(x => x.DocumentUrl).NotEmpty().WithMessage("DocumentUrl is required.");
    }
}

/// <summary>
/// Handler for SubmitKycCommand.
/// </summary>
public sealed class SubmitKycCommandHandler : IRequestHandler<SubmitKycCommand, SubmitKycResponseDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="SubmitKycCommandHandler"/>.
    /// </summary>
    public SubmitKycCommandHandler(IApplicationDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<SubmitKycResponseDto> Handle(SubmitKycCommand request, CancellationToken cancellationToken)
    {
        var callerId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerId))
        {
            throw new UnauthorizedAccessException("User authentication context is required.");
        }

        // Ordinary users can only submit KYC documents for their own profile
        if (!string.Equals(callerId, request.UserId, StringComparison.Ordinal))
        {
            var admin = await _dbContext.AdminProfiles
                .FirstOrDefaultAsync(a => a.UserId == callerId && a.IsActive && !a.IsDeleted, cancellationToken);

            if (admin == null || (admin.Role != AdminRoleType.SuperAdmin &&
                                  admin.Role != AdminRoleType.Admin &&
                                  !admin.HasPermission(Permissions.KycReview)))
            {
                throw new UnauthorizedAccessException("Caller is not authorized to submit KYC documents on behalf of another user.");
            }
        }

        var doc = new KycDocument(request.UserId, request.DocumentType, request.DocumentNumber, request.DocumentUrl);
        _dbContext.KycDocuments.Add(doc);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SubmitKycResponseDto(
            doc.Id, doc.UserId, doc.DocumentType.ToString(), doc.DocumentNumber, doc.Status.ToString());
    }
}
