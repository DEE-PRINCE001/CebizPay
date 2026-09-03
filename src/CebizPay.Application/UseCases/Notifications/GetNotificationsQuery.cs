using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Notifications;

/// <summary>
/// Query to retrieve a paginated inbox of in-app notifications for the authenticated caller.
/// </summary>
public sealed record GetNotificationsQuery(
    bool? IsRead = null,
    Guid? OrganizationId = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<InAppNotificationDto>>;

/// <summary>
/// Validator for GetNotificationsQuery.
/// </summary>
public sealed class GetNotificationsQueryValidator : AbstractValidator<GetNotificationsQuery>
{
    /// <summary>
    /// Initializes validation rules for GetNotificationsQuery.
    /// </summary>
    public GetNotificationsQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("PageNumber must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for GetNotificationsQuery.
/// </summary>
public sealed class GetNotificationsQueryHandler : IRequestHandler<GetNotificationsQuery, PagedResult<InAppNotificationDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetNotificationsQueryHandler"/>.
    /// </summary>
    public GetNotificationsQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
    }

    /// <inheritdoc/>
    public async Task<PagedResult<InAppNotificationDto>> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        if (request.OrganizationId.HasValue)
        {
            var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId.Value, cancellationToken);
            if (!hasAccess)
            {
                throw new UnauthorizedAccessException($"Tenant isolation check failed: No access to organization '{request.OrganizationId.Value}'.");
            }
        }

        var query = _dbContext.InAppNotifications
            .Where(n => n.UserId == callerUserId);

        if (request.IsRead.HasValue)
        {
            query = request.IsRead.Value
                ? query.Where(n => n.ReadAtUtc != null)
                : query.Where(n => n.ReadAtUtc == null);
        }

        if (request.OrganizationId.HasValue)
        {
            query = query.Where(n => n.OrganizationId == request.OrganizationId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .ThenByDescending(n => n.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(n => new InAppNotificationDto(
            n.Id,
            n.UserId,
            n.OrganizationId,
            n.Type,
            n.Title,
            n.Body,
            n.Priority,
            n.DeepLink,
            n.CreatedAtUtc,
            n.ReadAtUtc,
            n.IsRead)).ToList();

        return new PagedResult<InAppNotificationDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}
