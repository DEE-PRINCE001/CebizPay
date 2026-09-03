namespace CebizPay.Domain.Support.Enums;

/// <summary>
/// States for the deterministic Kola chatbot interaction state machine.
/// </summary>
public enum KolaSessionState
{
    /// <summary>Session started; presenting 6 root triage categories.</summary>
    Started = 1,

    /// <summary>Root category selected; presenting numbered sub-issues.</summary>
    CategorySelected = 2,

    /// <summary>Specific sub-issue selected; evaluating guidance or requesting additional context.</summary>
    IssueSelected = 3,

    /// <summary>Specific reference or information requested from the user.</summary>
    InformationRequested = 4,

    /// <summary>Self-service resolution steps suggested to the user.</summary>
    ResolutionSuggested = 5,

    /// <summary>Session escalated due to keyword match, user request, or critical financial classification.</summary>
    Escalated = 6,

    /// <summary>User confirmed resolution of inquiry without ticket creation.</summary>
    Resolved = 7,

    /// <summary>Support ticket successfully created from session for operator review.</summary>
    TicketCreated = 8
}
