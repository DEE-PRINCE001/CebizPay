using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Permissions;
using CebizPay.Application.Common.Extensions;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Manage;

/// <summary>
/// Query to retrieve a paginated directory of administrative profiles with identity metadata.
/// </summary>
public sealed record GetAdminDirectoryQuery(
    AdminRoleType? Role = null,
    bool? IsActive = null,
    string? Search = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<AdminProfileDto>>;

/// <summary>
/// Validator for GetAdminDirectoryQuery.
/// </summary>
public sealed class GetAdminDirectoryQueryValidator : AbstractValidator<GetAdminDirectoryQuery>
{
    /// <summary>
    /// Initializes validation rules for GetAdminDirectoryQuery.
    /// </summary>
    public GetAdminDirectoryQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("PageNumber must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for GetAdminDirectoryQuery.
/// </summary>
public sealed class GetAdminDirectoryQueryHandler : IRequestHandler<GetAdminDirectoryQuery, PagedResult<AdminProfileDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityService _identityService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetAdminDirectoryQueryHandler"/>.
    /// </summary>
    public GetAdminDirectoryQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IIdentityService identityService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
    }

    /// <inheritdoc/>
    public async Task<PagedResult<AdminProfileDto>> Handle(GetAdminDirectoryQuery request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        // Verify caller is an active non-deleted admin with Admins.View permission
        var callerAdmin = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == callerUserId && !a.IsDeleted && a.IsActive, cancellationToken);

        if (callerAdmin == null || !callerAdmin.HasPermission(Permissions.AdminsView))
        {
            throw new UnauthorizedAccessException("Insufficient permissions to view the administrative directory.");
        }

        var query = _dbContext.AdminProfiles
            .Where(a => !a.IsDeleted);

        if (request.Role.HasValue)
        {
            query = query.Where(a => a.Role == request.Role.Value);
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(a => a.IsActive == request.IsActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var profiles = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // Fetch user identity metadata
        var userIds = profiles.Select(p => p.UserId).Distinct().ToList();
        var userDetailsMap = await _identityService.GetUserDetailsByIdsAsync(userIds, cancellationToken);

        var items = new List<AdminProfileDto>();
        var search = request.Search?.Trim().ToLowerInvariant();

        foreach (var profile in profiles)
        {
            var email = profile.UserId;
            string? phone = null;

            if (userDetailsMap.TryGetValue(profile.UserId, out var details))
            {
                email = details.Email;
                phone = details.PhoneNumber;
            }

            // Apply search filter if specified
            if (!string.IsNullOrWhiteSpace(search))
            {
                var matchesEmail = email.Contains(search, StringComparison.OrdinalIgnoreCase);
                var matchesUserId = profile.UserId.Contains(search, StringComparison.OrdinalIgnoreCase);
                var matchesRole = profile.Role.ToString().Contains(search, StringComparison.OrdinalIgnoreCase);

                if (!matchesEmail && !matchesUserId && !matchesRole)
                {
                    continue;
                }
            }

            items.Add(new AdminProfileDto(
                profile.Id,
                profile.UserId,
                email,
                phone,
                profile.Role.ToString(),
                profile.IsActive,
                profile.IsMfaEnabled,
                profile.PermissionsList,
                profile.CreatedAtUtc,
                profile.UpdatedAtUtc));
        }

        return new PagedResult<AdminProfileDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}
