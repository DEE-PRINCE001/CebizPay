using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Entities;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.RegisterStep2;

/// <summary>
/// Handler for RegisterStep2Command.
/// </summary>
public sealed class RegisterStep2CommandHandler : IRequestHandler<RegisterStep2Command, RegisterStep2ResponseDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="RegisterStep2CommandHandler"/>.
    /// </summary>
    public RegisterStep2CommandHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    /// <inheritdoc/>
    public async Task<RegisterStep2ResponseDto> Handle(RegisterStep2Command request, CancellationToken cancellationToken)
    {
        // Enforce Tenant Authorization: verify user has access to request.OrganizationId
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed. Access to organization {request.OrganizationId} is forbidden.");
        }

        var org = await _dbContext.Organizations
            .FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Organization with ID {request.OrganizationId} was not found.");

        org.CompleteStep2(request.CacNumber, request.LogoUrl, request.CacCertificateUrl);

        var kybStep2 = new KybDetail(
            org.Id, 2, org.CompanyName, org.Email, org.Phone,
            request.CacNumber, request.LogoUrl, request.CacCertificateUrl);

        _dbContext.KybDetails.Add(kybStep2);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RegisterStep2ResponseDto(
            org.Id, org.CacNumber ?? string.Empty, org.Status.ToString(), org.KybStatus.ToString());
    }
}
