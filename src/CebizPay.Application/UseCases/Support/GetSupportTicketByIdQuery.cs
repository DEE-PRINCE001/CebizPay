using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Support.Entities;
using MediatR;

namespace CebizPay.Application.UseCases.Support;

/// <summary>
/// Query to retrieve a single support ticket by ID with strict ownership and IDOR protection.
/// </summary>
public sealed record GetSupportTicketByIdQuery(
    Guid TicketId) : IRequest<SupportTicketDto>;

/// <summary>
/// Handler for GetSupportTicketByIdQuery.
/// </summary>
public sealed class GetSupportTicketByIdQueryHandler : IRequestHandler<GetSupportTicketByIdQuery, SupportTicketDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetSupportTicketByIdQueryHandler"/>.
    /// </summary>
    public GetSupportTicketByIdQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<SupportTicketDto> Handle(GetSupportTicketByIdQuery request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var ticket = await _dbContext.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken)
            ?? throw new KeyNotFoundException($"Support ticket '{request.TicketId}' not found.");

        // Check ownership
        if (!string.Equals(ticket.UserId, callerUserId, StringComparison.OrdinalIgnoreCase))
        {
            // If not owner, verify administrative oversight authority
            var adminProfile = await _dbContext.AdminProfiles
                .FirstOrDefaultAsync(a => a.UserId == callerUserId && !a.IsDeleted && a.IsActive, cancellationToken);

            if (adminProfile == null || (adminProfile.Role != AdminRoleType.SuperAdmin && adminProfile.Role != AdminRoleType.Auditor))
            {
                // Prevent IDOR by disguising unauthorized inquiry as non-existent
                throw new KeyNotFoundException($"Support ticket '{request.TicketId}' not found.");
            }
        }

        var messages = await _dbContext.TicketMessages
            .Where(m => m.TicketId == ticket.Id)
            .OrderBy(m => m.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return MapToDto(ticket, messages);
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
