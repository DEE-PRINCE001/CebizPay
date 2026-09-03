using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Communication.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Announcements;

/// <summary>
/// Query to retrieve a paginated feed of published platform-wide announcements.
/// </summary>
public sealed record GetPlatformAnnouncementsQuery(
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<AnnouncementDto>>;

/// <summary>
/// Validator for GetPlatformAnnouncementsQuery.
/// </summary>
public sealed class GetPlatformAnnouncementsQueryValidator : AbstractValidator<GetPlatformAnnouncementsQuery>
{
    /// <summary>
    /// Initializes validation rules for GetPlatformAnnouncementsQuery.
    /// </summary>
    public GetPlatformAnnouncementsQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("PageNumber must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for GetPlatformAnnouncementsQuery.
/// </summary>
public sealed class GetPlatformAnnouncementsQueryHandler : IRequestHandler<GetPlatformAnnouncementsQuery, PagedResult<AnnouncementDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetPlatformAnnouncementsQueryHandler"/>.
    /// </summary>
    public GetPlatformAnnouncementsQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<PagedResult<AnnouncementDto>> Handle(GetPlatformAnnouncementsQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var query = _dbContext.Announcements
            .Where(a => a.Scope == AnnouncementScope.Platform && a.Status == AnnouncementStatus.Published);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.PublishedAtUtc)
            .ThenByDescending(a => a.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(a => new AnnouncementDto(
            a.Id,
            a.OrganizationId,
            null,
            a.Title,
            a.Description,
            a.Scope,
            a.Status,
            a.PublishedAtUtc,
            a.PublishedByUserId,
            a.CreatedAtUtc,
            a.CreatedByUserId,
            a.UpdatedAtUtc,
            a.UpdatedByUserId,
            a.ArchivedAtUtc,
            a.ArchivedByUserId)).ToList();

        return new PagedResult<AnnouncementDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}
