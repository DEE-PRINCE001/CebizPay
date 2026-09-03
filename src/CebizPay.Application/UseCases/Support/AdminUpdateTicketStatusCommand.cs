using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Support.Entities;
using CebizPay.Domain.Support.Enums;
using CebizPay.Domain.Support.Events;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Support;

/// <summary>
/// Command for administrative operator to update ticket status.
/// Restricted strictly to SuperAdmin.
/// </summary>
public sealed record AdminUpdateTicketStatusCommand(
    Guid TicketId,
    SupportTicketStatus Status,
    string? ResolutionSummary = null) : IRequest<SupportTicketDto>;

/// <summary>
/// Validator for AdminUpdateTicketStatusCommand.
/// </summary>
public sealed class AdminUpdateTicketStatusCommandValidator : AbstractValidator<AdminUpdateTicketStatusCommand>
{
    /// <summary>
    /// Initializes validation rules for AdminUpdateTicketStatusCommand.
    /// </summary>
    public AdminUpdateTicketStatusCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        When(x => x.Status == SupportTicketStatus.Resolved, () =>
        {
            RuleFor(x => x.ResolutionSummary)
                .NotEmpty().WithMessage("Resolution summary is required when resolving a ticket.");
        });
    }
}

/// <summary>
/// Handler for AdminUpdateTicketStatusCommand.
/// </summary>
public sealed class AdminUpdateTicketStatusCommandHandler : IRequestHandler<AdminUpdateTicketStatusCommand, SupportTicketDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly IOutboxService? _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="AdminUpdateTicketStatusCommandHandler"/>.
    /// </summary>
    public AdminUpdateTicketStatusCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        IOutboxService? outboxService = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
        _outboxService = outboxService;
    }

    /// <inheritdoc/>
    public async Task<SupportTicketDto> Handle(AdminUpdateTicketStatusCommand request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var adminProfile = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == callerUserId && !a.IsDeleted && a.IsActive, cancellationToken);

        if (adminProfile == null || adminProfile.Role != AdminRoleType.SuperAdmin)
        {
            throw new UnauthorizedAccessException("SuperAdmin authorization required to modify ticket state.");
        }

        var ticket = await _dbContext.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken)
            ?? throw new KeyNotFoundException($"Support ticket '{request.TicketId}' not found.");

        var now = DateTime.UtcNow;
        var previousStatus = ticket.Status;

        switch (request.Status)
        {
            case SupportTicketStatus.InReview:
                ticket.MarkInReview(now);
                break;
            case SupportTicketStatus.Resolved:
                ticket.Resolve(request.ResolutionSummary!, now);
                _outboxService?.Write(new SupportTicketResolvedDomainEvent(
                    TicketId: ticket.Id,
                    TicketNumber: ticket.TicketNumber,
                    UserId: ticket.UserId,
                    ResolutionSummary: ticket.ResolutionSummary!,
                    OccurredOnUtc: now));
                break;
            case SupportTicketStatus.Closed:
                ticket.Close(now);
                break;
            case SupportTicketStatus.Cancelled:
                ticket.Cancel(now);
                break;
            case SupportTicketStatus.Escalated:
                ticket.Escalate(now, request.ResolutionSummary);
                _outboxService?.Write(new SupportTicketEscalatedDomainEvent(
                    TicketId: ticket.Id,
                    TicketNumber: ticket.TicketNumber,
                    UserId: ticket.UserId,
                    Priority: ticket.Priority,
                    OccurredOnUtc: now));
                break;
            case SupportTicketStatus.Open:
                if (previousStatus == SupportTicketStatus.Resolved)
                {
                    ticket.Reopen(now);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request), $"Unsupported status: {request.Status}");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            action: request.Status == SupportTicketStatus.Resolved ? AuditActions.SupportTicketResolved : AuditActions.SupportTicketInReview,
            resourceType: AuditResourceTypes.SupportTicket,
            resourceId: ticket.Id.ToString(),
            organizationId: ticket.OrganizationId,
            details: $"Support ticket '{ticket.TicketNumber}' status updated from '{previousStatus}' to '{ticket.Status}' by admin '{callerUserId}'",
            cancellationToken: cancellationToken);

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
