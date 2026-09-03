using CebizPay.Domain.Support.Enums;

namespace CebizPay.Application.Common.Interfaces.Support;

/// <summary>
/// Service contract for the deterministic Kola support triage chatbot.
/// Guides users through structured triage, provides self-service resolutions,
/// detects human escalation keywords, detects critical financial issues, and creates support tickets.
/// </summary>
public interface IKolaChatbotService
{
    /// <summary>
    /// Initiates a new Kola chatbot triage session presenting the 6 root categories.
    /// </summary>
    KolaSessionResponse StartSession(string userId, Guid? organizationId = null);

    /// <summary>
    /// Processes user selection or natural input against the deterministic Kola state machine.
    /// </summary>
    Task<KolaSessionResponse> ProcessInputAsync(
        KolaSessionInput input,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Input payload for advancing a Kola triage session.
/// </summary>
public sealed record KolaSessionInput(
    string SessionId,
    string UserId,
    Guid? OrganizationId,
    KolaSessionState CurrentState,
    SupportTicketCategory? Category,
    int? SelectedIssueIndex,
    string UserMessage);

/// <summary>
/// State and presentation response from a Kola triage interaction.
/// </summary>
public sealed record KolaSessionResponse(
    string SessionId,
    KolaSessionState State,
    SupportTicketCategory? Category,
    string BotMessage,
    List<string> Options,
    bool IsEscalated,
    SupportTicketPriority Priority,
    Guid? CreatedTicketId = null,
    string? CreatedTicketNumber = null);
