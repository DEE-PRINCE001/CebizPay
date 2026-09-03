using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Interfaces.Support;
using CebizPay.Domain.Support.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Support;

/// <summary>
/// Command to progress an existing Kola chatbot triage session.
/// </summary>
public sealed record InteractKolaSessionCommand(
    string SessionId,
    KolaSessionState CurrentState,
    SupportTicketCategory? Category,
    int? SelectedIssueIndex,
    string Message,
    Guid? OrganizationId = null) : IRequest<KolaSessionResponse>;

/// <summary>
/// Validator for InteractKolaSessionCommand.
/// </summary>
public sealed class InteractKolaSessionCommandValidator : AbstractValidator<InteractKolaSessionCommand>
{
    /// <summary>
    /// Initializes validation rules for InteractKolaSessionCommand.
    /// </summary>
    public InteractKolaSessionCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Message).NotNull();
    }
}

/// <summary>
/// Handler for InteractKolaSessionCommand.
/// </summary>
public sealed class InteractKolaSessionCommandHandler : IRequestHandler<InteractKolaSessionCommand, KolaSessionResponse>
{
    private readonly IKolaChatbotService _kolaChatbotService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="InteractKolaSessionCommandHandler"/>.
    /// </summary>
    public InteractKolaSessionCommandHandler(
        IKolaChatbotService kolaChatbotService,
        ICurrentUserService currentUserService,
        ICurrentOrganizationContext orgContext)
    {
        _kolaChatbotService = kolaChatbotService ?? throw new ArgumentNullException(nameof(kolaChatbotService));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
    }

    /// <inheritdoc/>
    public async Task<KolaSessionResponse> Handle(InteractKolaSessionCommand request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var orgId = request.OrganizationId ?? _orgContext.CurrentOrganizationId;

        var input = new KolaSessionInput(
            SessionId: request.SessionId,
            UserId: callerUserId,
            OrganizationId: orgId,
            CurrentState: request.CurrentState,
            Category: request.Category,
            SelectedIssueIndex: request.SelectedIssueIndex,
            UserMessage: request.Message);

        return await _kolaChatbotService.ProcessInputAsync(input, cancellationToken);
    }
}
