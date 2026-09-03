using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Support.Entities;
using CebizPay.Domain.Support.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Support;

/// <summary>
/// Query to retrieve a paginated list of tickets owned by the authenticated customer.
/// </summary>
public sealed record GetSupportTicketsQuery(
    SupportTicketStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<SupportTicketDto>>;

/// <summary>
/// Validator for GetSupportTicketsQuery.
/// </summary>
public sealed class GetSupportTicketsQueryValidator : AbstractValidator<GetSupportTicketsQuery>
{
    /// <summary>
    /// Initializes validation rules for GetSupportTicketsQuery.
    /// </summary>
    public GetSupportTicketsQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

/// <summary>
/// Handler for GetSupportTicketsQuery.
/// </summary>
public sealed class GetSupportTicketsQueryHandler : IRequestHandler<GetSupportTicketsQuery, PagedResult<SupportTicketDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetSupportTicketsQueryHandler"/>.
    /// </summary>
    public GetSupportTicketsQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
    }

    /// <inheritdoc/>
    public async Task<PagedResult<SupportTicketDto>> Handle(GetSupportTicketsQuery request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var orgId = _orgContext.CurrentOrganizationId;

        // Strict ownership & tenant isolation
        var query = _dbContext.SupportTickets
            .Where(t => t.UserId == callerUserId && t.OrganizationId == orgId);

        if (request.Status.HasValue)
        {
            query = query.Where(t => t.Status == request.Status.Value);
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
