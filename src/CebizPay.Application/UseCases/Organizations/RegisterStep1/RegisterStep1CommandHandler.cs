using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Events;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.RegisterStep1;

/// <summary>
/// Handler for RegisterStep1Command.
/// </summary>
public sealed class RegisterStep1CommandHandler : IRequestHandler<RegisterStep1Command, RegisterStep1ResponseDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IEventPublisher _eventPublisher;

    /// <summary>
    /// Initializes a new instance of <see cref="RegisterStep1CommandHandler"/>.
    /// </summary>
    public RegisterStep1CommandHandler(IApplicationDbContext dbContext, IEventPublisher eventPublisher)
    {
        _dbContext = dbContext;
        _eventPublisher = eventPublisher;
    }

    /// <inheritdoc/>
    public async Task<RegisterStep1ResponseDto> Handle(RegisterStep1Command request, CancellationToken cancellationToken)
    {
        var targetEmail = request.Email.Trim().ToLowerInvariant();
        var targetName = request.CompanyName.Trim().ToLowerInvariant();

#pragma warning disable CA1862, CA1304, CA1311 // Suppress EF Core LINQ translation analyzer warnings
        // Duplicate constraint check
        var exists = await _dbContext.Organizations.AnyAsync(
            o => o.Email == targetEmail || o.CompanyName.ToLower() == targetName,
            cancellationToken);
#pragma warning restore CA1862, CA1304, CA1311

        if (exists)
        {
            throw new InvalidOperationException("An organization with this email or company name already exists.");
        }

        var org = new Organization(request.CompanyName, request.Email, request.Phone);
        var membership = new OrganizationMembership(request.OwnerUserId, org.Id, MembershipRoleType.Owner);
        var kybDetail = new KybDetail(org.Id, 1, org.CompanyName, org.Email, org.Phone);

        // Update professional status of owner to Staff
        var profile = await _dbContext.IndividualProfiles
            .FirstOrDefaultAsync(p => p.UserId == request.OwnerUserId, cancellationToken);
        if (profile != null)
        {
            profile.UpdateProfessionalStatus(ProfessionalStatus.Staff);
        }

        _dbContext.Organizations.Add(org);
        _dbContext.OrganizationMemberships.Add(membership);
        _dbContext.KybDetails.Add(kybDetail);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _eventPublisher.PublishAsync(
            new OrganizationRegisteredDomainEvent(org.Id, org.CompanyName, org.Email, DateTime.UtcNow),
            cancellationToken);

        return new RegisterStep1ResponseDto(org.Id, org.CompanyName, org.Status.ToString(), org.KybStatus.ToString());
    }
}
