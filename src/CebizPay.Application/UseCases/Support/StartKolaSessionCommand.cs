using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Interfaces.Support;
using MediatR;

namespace CebizPay.Application.UseCases.Support;

/// <summary>
/// Command to initiate a new Kola chatbot support triage session.
/// </summary>
public sealed record StartKolaSessionCommand(
    Guid? OrganizationId = null) : IRequest<KolaSessionResponse>;

/// <summary>
/// Handler for StartKolaSessionCommand.
/// </summary>
public sealed class StartKolaSessionCommandHandler : IRequestHandler<StartKolaSessionCommand, KolaSessionResponse>
{
    private readonly IKolaChatbotService _kolaChatbotService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="StartKolaSessionCommandHandler"/>.
    /// </summary>
    public StartKolaSessionCommandHandler(
        IKolaChatbotService kolaChatbotService,
        ICurrentUserService currentUserService,
        ICurrentOrganizationContext orgContext)
    {
        _kolaChatbotService = kolaChatbotService ?? throw new ArgumentNullException(nameof(kolaChatbotService));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
    }

    /// <inheritdoc/>
    public Task<KolaSessionResponse> Handle(StartKolaSessionCommand request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var orgId = request.OrganizationId ?? _orgContext.CurrentOrganizationId;
        var response = _kolaChatbotService.StartSession(callerUserId, orgId);
        return Task.FromResult(response);
    }
}
