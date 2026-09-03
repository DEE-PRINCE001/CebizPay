using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Support.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Support;

/// <summary>
/// Command for administrative operator to post a response message to a customer support ticket.
/// Restricted to SuperAdmin.
/// </summary>
public sealed record AdminAddTicketMessageCommand(
    Guid TicketId,
    string Content) : IRequest<TicketMessageDto>;

/// <summary>
/// Validator for AdminAddTicketMessageCommand.
/// </summary>
public sealed class AdminAddTicketMessageCommandValidator : AbstractValidator<AdminAddTicketMessageCommand>
{
    /// <summary>
    /// Initializes validation rules for AdminAddTicketMessageCommand.
    /// </summary>
    public AdminAddTicketMessageCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Response content is required.")
            .MaximumLength(4000).WithMessage("Response content cannot exceed 4,000 characters.");
    }
}

/// <summary>
/// Handler for AdminAddTicketMessageCommand.
/// </summary>
public sealed class AdminAddTicketMessageCommandHandler : IRequestHandler<AdminAddTicketMessageCommand, TicketMessageDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="AdminAddTicketMessageCommandHandler"/>.
    /// </summary>
    public AdminAddTicketMessageCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<TicketMessageDto> Handle(AdminAddTicketMessageCommand request, CancellationToken cancellationToken)
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
            throw new UnauthorizedAccessException("SuperAdmin authorization required to post operator responses.");
        }

        var ticket = await _dbContext.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken)
            ?? throw new KeyNotFoundException($"Support ticket '{request.TicketId}' not found.");

        var now = DateTime.UtcNow;
        var message = ticket.AddMessage(callerUserId, TicketMessageSenderType.Admin, request.Content, now);
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
