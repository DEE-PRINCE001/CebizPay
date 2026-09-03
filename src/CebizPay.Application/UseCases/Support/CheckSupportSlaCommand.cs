using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Support.Enums;
using CebizPay.Domain.Support.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CebizPay.Application.UseCases.Support;

/// <summary>
/// Command executed by background worker to detect and record 12-hour review SLA breaches.
/// </summary>
public sealed record CheckSupportSlaCommand(
    int BatchSize = 100) : IRequest<int>;

/// <summary>
/// Handler for CheckSupportSlaCommand.
/// </summary>
public sealed class CheckSupportSlaCommandHandler : IRequestHandler<CheckSupportSlaCommand, int>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;
    private readonly IOutboxService? _outboxService;
    private readonly ILogger<CheckSupportSlaCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="CheckSupportSlaCommandHandler"/>.
    /// </summary>
    public CheckSupportSlaCommandHandler(
        IApplicationDbContext dbContext,
        IAuditLogService auditLogService,
        ILogger<CheckSupportSlaCommandHandler> logger,
        IOutboxService? outboxService = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _outboxService = outboxService;
    }

    /// <inheritdoc/>
    public async Task<int> Handle(CheckSupportSlaCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // Bounded query for active tickets whose 12-hour SLA expired and breach has not yet been recorded
        var breachedTickets = await _dbContext.SupportTickets
            .Where(t => !t.IsSlaBreached &&
                        t.SlaDueAtUtc <= now &&
                        t.Status != SupportTicketStatus.Resolved &&
                        t.Status != SupportTicketStatus.Closed &&
                        t.Status != SupportTicketStatus.Cancelled)
            .OrderBy(t => t.SlaDueAtUtc)
            .Take(request.BatchSize)
            .ToListAsync(cancellationToken);

        if (breachedTickets.Count == 0)
        {
            return 0;
        }

        foreach (var ticket in breachedTickets)
        {
            ticket.MarkSlaBreached(now);

            _outboxService?.Write(new SupportTicketSlaBreachedDomainEvent(
                TicketId: ticket.Id,
                TicketNumber: ticket.TicketNumber,
                UserId: ticket.UserId,
                SlaDueAtUtc: ticket.SlaDueAtUtc,
                OccurredOnUtc: now));

            await _auditLogService.LogAsync(
                action: AuditActions.SupportTicketSlaBreached,
                resourceType: AuditResourceTypes.SupportTicket,
                resourceId: ticket.Id.ToString(),
                organizationId: ticket.OrganizationId,
                details: $"Ticket '{ticket.TicketNumber}' breached its 12-hour review SLA (Due: {ticket.SlaDueAtUtc:O})",
                cancellationToken: cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return breachedTickets.Count;
    }
}
