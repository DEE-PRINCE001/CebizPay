using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Support.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Support;

/// <summary>
/// Command for customer to append a message to their ticket thread.
/// </summary>
public sealed record AddTicketMessageCommand(
    Guid TicketId,
    string Content) : IRequest<TicketMessageDto>;

/// <summary>
/// Validator for AddTicketMessageCommand.
/// </summary>
public sealed class AddTicketMessageCommandValidator : AbstractValidator<AddTicketMessageCommand>
{
    /// <summary>
    /// Initializes validation rules for AddTicketMessageCommand.
    /// </summary>
    public AddTicketMessageCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Message content is required.")
            .MaximumLength(4000).WithMessage("Message cannot exceed 4,000 characters.");
    }
}

/// <summary>
/// Handler for AddTicketMessageCommand.
/// </summary>
public sealed class AddTicketMessageCommandHandler : IRequestHandler<AddTicketMessageCommand, TicketMessageDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="AddTicketMessageCommandHandler"/>.
    /// </summary>
    public AddTicketMessageCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<TicketMessageDto> Handle(AddTicketMessageCommand request, CancellationToken cancellationToken)
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
        var message = ticket.AddMessage(callerUserId, TicketMessageSenderType.Customer, request.Content, now);
        _dbContext.TicketMessages.Add(message);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new TicketMessageDto(
            Id: message.Id,
            TicketId: message.TicketId,
            SenderUserId: message.SenderUserId,
            SenderType: message.SenderType,
            Content: message.Content,
            CreatedAtUtc: message.CreatedAtUtc);
    }
}
