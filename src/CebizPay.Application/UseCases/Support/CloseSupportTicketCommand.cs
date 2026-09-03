using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Support.Entities;
using CebizPay.Domain.Support.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Support;

/// <summary>
/// Command for a customer to close their own support ticket.
/// </summary>
public sealed record CloseSupportTicketCommand(
    Guid TicketId) : IRequest<bool>;

/// <summary>
/// Handler for CloseSupportTicketCommand.
/// </summary>
public sealed class CloseSupportTicketCommandHandler : IRequestHandler<CloseSupportTicketCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    /// <summary>
    /// Initializes a new instance of <see cref="CloseSupportTicketCommandHandler"/>.
    /// </summary>
    public CloseSupportTicketCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
    }

    /// <inheritdoc/>
    public async Task<bool> Handle(CloseSupportTicketCommand request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var ticket = await _dbContext.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken)
            ?? throw new KeyNotFoundException($"Support ticket '{request.TicketId}' not found.");

        if (!string.Equals(ticket.UserId, callerUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new KeyNotFoundException($"Support ticket '{request.TicketId}' not found.");
        }

        var now = DateTime.UtcNow;
        ticket.Close(now);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            action: AuditActions.SupportTicketClosed,
            resourceType: AuditResourceTypes.SupportTicket,
            resourceId: ticket.Id.ToString(),
            organizationId: ticket.OrganizationId,
            details: $"Support ticket '{ticket.TicketNumber}' closed by customer '{callerUserId}'",
            cancellationToken: cancellationToken);

        return true;
    }
}
