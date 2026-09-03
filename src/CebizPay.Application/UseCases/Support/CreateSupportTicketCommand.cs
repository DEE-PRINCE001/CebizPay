using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Interfaces.Support;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Support.Entities;
using CebizPay.Domain.Support.Enums;
using CebizPay.Domain.Support.Events;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Support;

/// <summary>
/// Command to open a new support ticket (supports direct and offline synchronized submissions).
/// </summary>
public sealed record CreateSupportTicketCommand(
    SupportTicketCategory Category,
    string Subject,
    string Description,
    SupportTicketPriority Priority = SupportTicketPriority.Normal,
    string? IdempotencyKey = null) : IRequest<SupportTicketDto>;

/// <summary>
/// Validator for CreateSupportTicketCommand.
/// </summary>
public sealed class CreateSupportTicketCommandValidator : AbstractValidator<CreateSupportTicketCommand>
{
    /// <summary>
    /// Initializes validation rules for CreateSupportTicketCommand.
    /// </summary>
    public CreateSupportTicketCommandValidator()
    {
        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject is required.")
            .MaximumLength(250).WithMessage("Subject cannot exceed 250 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(4000).WithMessage("Description cannot exceed 4,000 characters.");
    }
}

/// <summary>
/// Handler for CreateSupportTicketCommand.
/// </summary>
public sealed class CreateSupportTicketCommandHandler : IRequestHandler<CreateSupportTicketCommand, SupportTicketDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ISupportTicketNumberGenerator _ticketNumberGenerator;
    private readonly IAuditLogService _auditLogService;
    private readonly IOutboxService? _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="CreateSupportTicketCommandHandler"/>.
    /// </summary>
    public CreateSupportTicketCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ICurrentOrganizationContext orgContext,
        ISupportTicketNumberGenerator ticketNumberGenerator,
        IAuditLogService auditLogService,
        IOutboxService? outboxService = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
        _ticketNumberGenerator = ticketNumberGenerator ?? throw new ArgumentNullException(nameof(ticketNumberGenerator));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
        _outboxService = outboxService;
    }

    /// <inheritdoc/>
    public async Task<SupportTicketDto> Handle(CreateSupportTicketCommand request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        // 1. Idempotency verification for offline synchronization retries
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await _dbContext.SupportTickets
                .FirstOrDefaultAsync(t => t.UserId == callerUserId && t.IdempotencyKey == request.IdempotencyKey.Trim(), cancellationToken);

            if (existing != null)
            {
                var existingMessages = await _dbContext.TicketMessages
                    .Where(m => m.TicketId == existing.Id)
                    .OrderBy(m => m.CreatedAtUtc)
                    .ToListAsync(cancellationToken);

                return MapToDto(existing, existingMessages);
            }
        }

        var orgId = _orgContext.CurrentOrganizationId;
        var now = DateTime.UtcNow;
        var ticketNumber = _ticketNumberGenerator.GenerateTicketNumber();

        // 2. Instantiate and persist aggregate
        var ticket = SupportTicket.Create(
            ticketNumber: ticketNumber,
            userId: callerUserId,
            organizationId: orgId,
            category: request.Category,
            subject: request.Subject,
            description: request.Description,
            priority: request.Priority,
            now: now,
            idempotencyKey: request.IdempotencyKey);

        // Add initial message to thread
        ticket.AddMessage(callerUserId, TicketMessageSenderType.Customer, request.Description, now);

        _dbContext.SupportTickets.Add(ticket);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 3. Emit Domain Event to Outbox
        _outboxService?.Write(new SupportTicketCreatedDomainEvent(
            TicketId: ticket.Id,
            TicketNumber: ticket.TicketNumber,
            UserId: ticket.UserId,
            OrganizationId: ticket.OrganizationId,
            Category: ticket.Category,
            Priority: ticket.Priority,
            OccurredOnUtc: now));

        // 4. Record audit log
        await _auditLogService.LogAsync(
            action: AuditActions.SupportTicketCreated,
            resourceType: AuditResourceTypes.SupportTicket,
            resourceId: ticket.Id.ToString(),
            organizationId: orgId,
            details: $"Support ticket '{ticket.TicketNumber}' opened by '{callerUserId}'. Category: {ticket.Category}, Priority: {ticket.Priority}",
            cancellationToken: cancellationToken);

        var messages = await _dbContext.TicketMessages
            .Where(m => m.TicketId == ticket.Id)
            .OrderBy(m => m.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return MapToDto(ticket, messages);
    }

    private static SupportTicketDto MapToDto(SupportTicket ticket, IEnumerable<TicketMessage> messages)
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
            Messages: messages.OrderBy(m => m.CreatedAtUtc).Select(m => new TicketMessageDto(
                Id: m.Id,
                TicketId: m.TicketId,
                SenderUserId: m.SenderUserId,
                SenderType: m.SenderType,
                Content: m.Content,
                CreatedAtUtc: m.CreatedAtUtc)).ToList());
    }
}
