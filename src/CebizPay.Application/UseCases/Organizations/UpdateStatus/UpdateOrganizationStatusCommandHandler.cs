using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Application.UseCases.Organizations.UpdateStatus;

/// <summary>
/// Handler for UpdateOrganizationStatusCommand.
/// </summary>
public sealed class UpdateOrganizationStatusCommandHandler : IRequestHandler<UpdateOrganizationStatusCommand, UpdateOrganizationStatusResponseDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IEventPublisher _eventPublisher;

    /// <summary>
    /// Initializes a new instance of <see cref="UpdateOrganizationStatusCommandHandler"/>.
    /// </summary>
    public UpdateOrganizationStatusCommandHandler(IApplicationDbContext dbContext, IEventPublisher eventPublisher)
    {
        _dbContext = dbContext;
        _eventPublisher = eventPublisher;
    }

    /// <inheritdoc/>
    public async Task<UpdateOrganizationStatusResponseDto> Handle(UpdateOrganizationStatusCommand request, CancellationToken cancellationToken)
    {
        var org = await _dbContext.Organizations
            .FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Organization with ID {request.OrganizationId} was not found.");

        var oldStatus = org.Status;
        org.TransitionStatus(request.NewStatus, request.Reason);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _eventPublisher.PublishAsync(
            new OrganizationStatusChangedDomainEvent(
                org.Id, oldStatus, org.Status, request.Reason, DateTime.UtcNow),
            cancellationToken);

        return new UpdateOrganizationStatusResponseDto(org.Id, org.Status.ToString(), org.KybStatus.ToString());
    }
}
