using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Staff;

/// <summary>
/// Data transfer object representing a salary payment line item for a staff member.
/// </summary>
public sealed record StaffSalaryItemDto(
    Guid Id,
    Guid PayrollBatchId,
    decimal GrossPay,
    decimal TotalDeductions,
    decimal NetPay,
    string Currency,
    string Status,
    DateTime CreatedAtUtc,
    string? VoucherReference = null,
    string? PaymentMethod = null);

/// <summary>
/// Query to retrieve historical salary payment records for a staff member.
/// </summary>
public sealed record GetStaffSalariesQuery(
    Guid OrganizationId,
    Guid MembershipId,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<StaffSalaryItemDto>>;

/// <summary>
/// Validator for GetStaffSalariesQuery.
/// </summary>
public sealed class GetStaffSalariesQueryValidator : AbstractValidator<GetStaffSalariesQuery>
{
    /// <summary>
    /// Initializes validation rules.
    /// </summary>
    public GetStaffSalariesQueryValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.MembershipId).NotEmpty().WithMessage("MembershipId is required.");
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1).WithMessage("PageNumber must be at least 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for <see cref="GetStaffSalariesQuery"/>.
/// </summary>
public sealed class GetStaffSalariesQueryHandler : IRequestHandler<GetStaffSalariesQuery, PagedResult<StaffSalaryItemDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetStaffSalariesQueryHandler"/>.
    /// </summary>
    public GetStaffSalariesQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
    }

    /// <inheritdoc/>
    public async Task<PagedResult<StaffSalaryItemDto>> Handle(GetStaffSalariesQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken).ConfigureAwait(false);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var membership = await _dbContext.OrganizationMemberships
            .FirstOrDefaultAsync(m => m.Id == request.MembershipId && m.OrganizationId == request.OrganizationId, cancellationToken)
            .ConfigureAwait(false);

        if (membership == null)
        {
            throw new KeyNotFoundException($"Staff membership '{request.MembershipId}' was not found in this organization.");
        }

        var query = _dbContext.PayrollItems
            .Where(p => p.OrganizationId == request.OrganizationId && p.EmployeeUserId == membership.UserId);

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (items.Count == 0)
        {
            return new PagedResult<StaffSalaryItemDto>(Array.Empty<StaffSalaryItemDto>(), totalCount, request.PageNumber, request.PageSize);
        }

        var voucherIds = items
            .Where(i => i.PaymentVoucherId.HasValue)
            .Select(i => i.PaymentVoucherId!.Value)
            .Distinct()
            .ToList();

        var vouchersList = await _dbContext.PaymentVouchers
            .Where(v => voucherIds.Contains(v.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var voucherMap = vouchersList.ToDictionary(v => v.Id, v => v.VoucherReference);

        var dtos = items.Select(i =>
        {
            string? voucherRef = null;
            if (i.PaymentVoucherId.HasValue && voucherMap.TryGetValue(i.PaymentVoucherId.Value, out var vRef))
            {
                voucherRef = vRef;
            }

            return new StaffSalaryItemDto(
                Id: i.Id,
                PayrollBatchId: i.PayrollBatchId,
                GrossPay: i.GrossPay,
                TotalDeductions: i.TotalDeductions,
                NetPay: i.NetPay,
                Currency: i.Currency.ToString(),
                Status: i.Status.ToString(),
                CreatedAtUtc: i.CreatedAtUtc,
                VoucherReference: voucherRef,
                PaymentMethod: "Wallet");
        }).ToList();

        return new PagedResult<StaffSalaryItemDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}
