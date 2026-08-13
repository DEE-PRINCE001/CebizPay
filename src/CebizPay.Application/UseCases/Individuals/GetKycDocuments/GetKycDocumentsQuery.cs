using CebizPay.Application.Common.Interfaces.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Application.UseCases.Individuals.GetKycDocuments;

/// <summary>
/// Query to retrieve KYC documents for an individual user.
/// </summary>
public sealed record GetKycDocumentsQuery(string UserId) : IRequest<IEnumerable<KycDocumentDto>>;

/// <summary>
/// DTO representing a KYC document record.
/// </summary>
public sealed record KycDocumentDto(
    Guid Id,
    string UserId,
    string DocumentType,
    string DocumentNumber,
    string DocumentUrl,
    string Status,
    string? RejectionReason,
    DateTime SubmittedAtUtc,
    DateTime? ReviewedAtUtc);

/// <summary>
/// Handler for GetKycDocumentsQuery.
/// </summary>
public sealed class GetKycDocumentsQueryHandler : IRequestHandler<GetKycDocumentsQuery, IEnumerable<KycDocumentDto>>
{
    private readonly IApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetKycDocumentsQueryHandler"/>.
    /// </summary>
    public GetKycDocumentsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<KycDocumentDto>> Handle(GetKycDocumentsQuery request, CancellationToken cancellationToken)
    {
        var documents = await _dbContext.KycDocuments
            .Where(d => d.UserId == request.UserId)
            .OrderByDescending(d => d.SubmittedAtUtc)
            .Select(d => new KycDocumentDto(
                d.Id,
                d.UserId,
                d.DocumentType.ToString(),
                d.DocumentNumber,
                d.DocumentUrl,
                d.Status.ToString(),
                d.RejectionReason,
                d.SubmittedAtUtc,
                d.ReviewedAtUtc))
            .ToListAsync(cancellationToken);

        return documents;
    }
}
