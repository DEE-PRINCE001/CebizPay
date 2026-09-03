using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Support.Entities;
using CebizPay.Domain.Support.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Support;

/// <summary>
/// Query for administrative oversight to retrieve a filtered, paginated list of support tickets.
/// Authorized for SuperAdmin and Auditor.
/// </summary>
public sealed record AdminGetSupportTicketsQuery(
    SupportTicketStatus? Status = null,
    SupportTicketCategory? Category = null,
    SupportTicketPriority? Priority = null,
    bool? IsSlaBreached = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<SupportTicketDto>>;

/// <summary>
/// Validator for AdminGetSupportTicketsQuery.
/// </summary>
public sealed class AdminGetSupportTicketsQueryValidator : AbstractValidator<AdminGetSupportTicketsQuery>
{
    /// <summary>
    /// Initializes validation rules for AdminGetSupportTicketsQuery.
    /// </summary>
    public AdminGetSupportTicketsQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

/// <summary>
/// Handler for AdminGetSupportTicketsQuery.
/// </summary>
public sealed class AdminGetSupportTicketsQueryHandler : IRequestHandler<AdminGetSupportTicketsQuery, PagedResult<SupportTicketDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="AdminGetSupportTicketsQueryHandler"/>.
    /// </summary>
    public AdminGetSupportTicketsQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<PagedResult<SupportTicketDto>> Handle(AdminGetSupportTicketsQuery request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var adminProfile = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == callerUserId && !a.IsDeleted && a.IsActive, cancellationToken);

        if (adminProfile == null || (adminProfile.Role != AdminRoleType.SuperAdmin && adminProfile.Role != AdminRoleType.Auditor))
        {
            throw new UnauthorizedAccessException("Administrative support authorization required.");
        }

        var query = _dbContext.SupportTickets
            .AsQueryable();

        if (request.Status.HasValue)
        {
            query = query.Where(t => t.Status == request.Status.Value);
        }

        if (request.Category.HasValue)
        {
            query = query.Where(t => t.Category == request.Category.Value);
        }

        if (request.Priority.HasValue)
        {
            query = query.Where(t => t.Priority == request.Priority.Value);
        }

        if (request.IsSlaBreached.HasValue)
        {
            query = query.Where(t => t.IsSlaBreached == request.IsSlaBreached.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.CreatedAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var ticketIds = items.Select(t => t.Id).ToList();
        var allMessages = await _dbContext.TicketMessages
            .Where(m => ticketIds.Contains(m.TicketId))
            .OrderBy(m => m.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var messagesByTicket = allMessages.GroupBy(m => m.TicketId).ToDictionary(g => g.Key, g => g.ToList());

        var dtos = items.Select(t => MapToDto(t, messagesByTicket.GetValueOrDefault(t.Id) ?? new List<TicketMessage>())).ToList();

        return new PagedResult<SupportTicketDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }

    private static SupportTicketDto MapToDto(SupportTicket ticket, List<TicketMessage> messages)
    {
        return new SupportTicketDto(
            Id: ticket.Id,
            TicketNumber: ticket.TicketNumber,
            UserId: ticket.UserId,
            OrganizationId: ticket.OrganizationId,
            Category: ticket.Category,
            Subject: ticket.Subject,
            Description: ticket.Description,
            Status: ticket.Status,
            Priority: ticket.Priority,
            CreatedAtUtc: ticket.CreatedAtUtc,
            UpdatedAtUtc: ticket.UpdatedAtUtc,
            EscalatedAtUtc: ticket.EscalatedAtUtc,
            FirstResponseAtUtc: ticket.FirstResponseAtUtc,
            ResolvedAtUtc: ticket.ResolvedAtUtc,
            ClosedAtUtc: ticket.ClosedAtUtc,
            SlaDueAtUtc: ticket.SlaDueAtUtc,
            IsSlaBreached: ticket.IsSlaBreached,
            ResolutionSummary: ticket.ResolutionSummary,
            Messages: messages.Select(m => new TicketMessageDto(
                Id: m.Id,
                TicketId: m.TicketId,
                SenderUserId: m.SenderUserId,
                SenderType: m.SenderType,
                Content: m.Content,
                CreatedAtUtc: m.CreatedAtUtc)).ToList());
    }
}
