using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using FluentValidation;
using MediatR;

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

    /// <summary>
    /// Initializes a new instance of <see cref="SubmitKycCommandHandler"/>.
    /// </summary>
    public SubmitKycCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<SubmitKycResponseDto> Handle(SubmitKycCommand request, CancellationToken cancellationToken)
    {
        var doc = new KycDocument(request.UserId, request.DocumentType, request.DocumentNumber, request.DocumentUrl);
        _dbContext.KycDocuments.Add(doc);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SubmitKycResponseDto(
            doc.Id, doc.UserId, doc.DocumentType.ToString(), doc.DocumentNumber, doc.Status.ToString());
    }
}
