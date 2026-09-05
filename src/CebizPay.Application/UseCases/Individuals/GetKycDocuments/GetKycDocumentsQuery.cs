using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Permissions;
using CebizPay.Application.Common.Extensions;
using MediatR;

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
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetKycDocumentsQueryHandler"/>.
    /// </summary>
    public GetKycDocumentsQueryHandler(IApplicationDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<KycDocumentDto>> Handle(GetKycDocumentsQuery request, CancellationToken cancellationToken)
    {
        var callerId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerId))
        {
            throw new UnauthorizedAccessException("User authentication context is required.");
        }

        // Ordinary users can only access their own KYC documents
        if (!string.Equals(callerId, request.UserId, StringComparison.Ordinal))
        {
            var admin = await _dbContext.AdminProfiles
                .FirstOrDefaultAsync(a => a.UserId == callerId && a.IsActive && !a.IsDeleted, cancellationToken);

            if (admin == null || (admin.Role != AdminRoleType.SuperAdmin &&
                                  admin.Role != AdminRoleType.Admin &&
                                  !admin.HasPermission(Permissions.KycView) &&
                                  !admin.HasPermission(Permissions.KycReview)))
            {
                throw new UnauthorizedAccessException("Caller is not authorized to view KYC documents for another user.");
            }
        }

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
