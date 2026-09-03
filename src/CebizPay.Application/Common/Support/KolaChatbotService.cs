using System.Globalization;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Interfaces.Support;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Support.Entities;
using CebizPay.Domain.Support.Enums;
using CebizPay.Domain.Support.Events;
using MediatR;

namespace CebizPay.Application.Common.Support;

/// <summary>
/// Deterministic implementation of the Kola support triage chatbot.
/// Provides numbered issue triage, self-service resolutions, explicit human escalation,
/// and automatic critical security/financial incident routing.
/// </summary>
public sealed class KolaChatbotService : IKolaChatbotService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ISupportTicketNumberGenerator _ticketNumberGenerator;
    private readonly IAuditLogService _auditLogService;
    private readonly IOutboxService? _outboxService;

    private static readonly string[] RootCategories =
    [
        "1. Payment / Transfer",
        "2. Wallet / Account",
        "3. KYC / Verification",
        "4. Savings / Thrift",
        "5. Business / Workplace",
        "6. Something Else"
    ];

    private static readonly Dictionary<SupportTicketCategory, string[]> CategoryIssues = new()
    {
        [SupportTicketCategory.PaymentOrTransfer] =
        [
            "1. Transfer failed",
            "2. Transfer pending",
            "3. Money deducted but recipient did not receive it",
            "4. I don't recognize this transaction" // CRITICAL
        ],
        [SupportTicketCategory.WalletOrAccount] =
        [
            "1. Can't access my account",
            "2. Wallet balance looks wrong", // CRITICAL
            "3. Deposit/funding problem",
            "4. Account suspended/restricted"
        ],
        [SupportTicketCategory.KycOrVerification] =
        [
            "1. KYC verification failed",
            "2. KYC still pending",
            "3. I need to update my information"
        ],
        [SupportTicketCategory.SavingsOrThrift] =
        [
            "1. Contribution problem",
            "2. Payout problem",
            "3. Cycle/group problem",
            "4. Something is wrong with my thrift account"
        ],
        [SupportTicketCategory.BusinessOrWorkplace] =
        [
            "1. Payroll problem",
            "2. Expense/reimbursement problem",
            "3. Invoice/business transaction problem",
            "4. Workplace account problem"
        ],
        [SupportTicketCategory.Other] =
        [
            "1. General inquiry",
            "2. Speak to human representative"
        ]
    };

    private static readonly string[] HumanEscalationKeywords =
    [
        "human", "human agent", "representative", "someone", "agent", "live agent",
        "operator", "speak to someone", "talk to human", "real person", "person"
    ];

    private static readonly string[] CriticalKeywords =
    [
        "unauthorized", "without authorization", "not authorized", "fraud", "scam", "stolen", "hacked", "balance discrepancy",
        "stolen money", "compromised", "don't recognize"
    ];

    /// <summary>
    /// Initializes a new instance of <see cref="KolaChatbotService"/>.
    /// </summary>
    public KolaChatbotService(
        IApplicationDbContext dbContext,
        ISupportTicketNumberGenerator ticketNumberGenerator,
        IAuditLogService auditLogService,
        IOutboxService? outboxService = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _ticketNumberGenerator = ticketNumberGenerator ?? throw new ArgumentNullException(nameof(ticketNumberGenerator));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
        _outboxService = outboxService;
    }

    /// <inheritdoc/>
    public KolaSessionResponse StartSession(string userId, Guid? organizationId = null)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var greeting = "Hello! I am Kola, your CebizPay automated assistant. Please select the category that best describes your inquiry:";

        return new KolaSessionResponse(
            SessionId: sessionId,
            State: KolaSessionState.Started,
            Category: null,
            BotMessage: greeting,
            Options: RootCategories.ToList(),
            IsEscalated: false,
            Priority: SupportTicketPriority.Normal);
    }

    /// <inheritdoc/>
    public async Task<KolaSessionResponse> ProcessInputAsync(
        KolaSessionInput input,
        CancellationToken cancellationToken = default)
    {
        var message = (input.UserMessage ?? string.Empty).Trim().ToLowerInvariant();

        // 1. Check restart
        if (message is "restart" or "start over" or "reset" or "menu")
        {
            return StartSession(input.UserId, input.OrganizationId);
        }

        // 2. Check explicit human escalation request
        if (ContainsAny(message, HumanEscalationKeywords))
        {
            return await EscalateToTicketAsync(
                input,
                input.Category ?? SupportTicketCategory.Other,
                string.IsNullOrWhiteSpace(input.UserMessage) ? "Customer Support Escalation" : input.UserMessage,
                SupportTicketPriority.High,
                "Customer explicitly requested live human operator assistance.",
                cancellationToken);
        }

        // 3. Check critical financial/security triggers in natural message
        if (ContainsAny(message, CriticalKeywords))
        {
            return await EscalateToTicketAsync(
                input,
                input.Category ?? SupportTicketCategory.WalletOrAccount,
                string.IsNullOrWhiteSpace(input.UserMessage) ? "Support Escalation" : input.UserMessage,
                SupportTicketPriority.Critical,
                "Critical financial or security discrepancy detected.",
                cancellationToken);
        }

        // 4. State Machine transitions
        switch (input.CurrentState)
        {
            case KolaSessionState.Started:
                return HandleCategorySelection(input, message);

            case KolaSessionState.CategorySelected:
                return await HandleIssueSelectionAsync(input, message, cancellationToken);

            case KolaSessionState.IssueSelected:
            case KolaSessionState.ResolutionSuggested:
                return await HandleResolutionStepAsync(input, message, cancellationToken);

            default:
                // Fallback / safe restart
                return StartSession(input.UserId, input.OrganizationId);
        }
    }

    private static KolaSessionResponse HandleCategorySelection(KolaSessionInput input, string message)
    {
        SupportTicketCategory? selectedCategory = null;

        if (message is "1" or "payment" or "transfer" or "payment / transfer")
            selectedCategory = SupportTicketCategory.PaymentOrTransfer;
        else if (message is "2" or "wallet" or "account" or "wallet / account")
            selectedCategory = SupportTicketCategory.WalletOrAccount;
        else if (message is "3" or "kyc" or "verification" or "kyc / verification")
            selectedCategory = SupportTicketCategory.KycOrVerification;
        else if (message is "4" or "savings" or "thrift" or "savings / thrift")
            selectedCategory = SupportTicketCategory.SavingsOrThrift;
        else if (message is "5" or "business" or "workplace" or "business / workplace")
            selectedCategory = SupportTicketCategory.BusinessOrWorkplace;
        else if (message is "6" or "something else" or "other" or "else")
            selectedCategory = SupportTicketCategory.Other;

        if (!selectedCategory.HasValue)
        {
            return new KolaSessionResponse(
                SessionId: input.SessionId,
                State: KolaSessionState.Started,
                Category: null,
                BotMessage: "I didn't quite understand that selection. Please select one of the numbered options below (1 to 6):",
                Options: RootCategories.ToList(),
                IsEscalated: false,
                Priority: SupportTicketPriority.Normal);
        }

        var issues = CategoryIssues[selectedCategory.Value].ToList();
        return new KolaSessionResponse(
            SessionId: input.SessionId,
            State: KolaSessionState.CategorySelected,
            Category: selectedCategory.Value,
            BotMessage: $"You selected '{selectedCategory}'. Please choose your specific issue:",
            Options: issues,
            IsEscalated: false,
            Priority: SupportTicketPriority.Normal);
    }

    private async Task<KolaSessionResponse> HandleIssueSelectionAsync(
        KolaSessionInput input,
        string message,
        CancellationToken cancellationToken)
    {
        var category = input.Category ?? SupportTicketCategory.Other;
        var issues = CategoryIssues[category];

        int issueIndex = -1;
        if (int.TryParse(message, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedIndex))
        {
            issueIndex = parsedIndex - 1;
        }
        else
        {
            for (int i = 0; i < issues.Length; i++)
            {
                if (issues[i].Contains(message, StringComparison.OrdinalIgnoreCase))
                {
                    issueIndex = i;
                    break;
                }
            }
        }

        if (issueIndex < 0 || issueIndex >= issues.Length)
        {
            return new KolaSessionResponse(
                SessionId: input.SessionId,
                State: KolaSessionState.CategorySelected,
                Category: category,
                BotMessage: "Please choose a valid numbered issue from the list below:",
                Options: issues.ToList(),
                IsEscalated: false,
                Priority: SupportTicketPriority.Normal);
        }

        // Evaluate critical financial issues
        if (category == SupportTicketCategory.PaymentOrTransfer && issueIndex == 3) // "I don't recognize this transaction"
        {
            return await EscalateToTicketAsync(
                input,
                category,
                "Unrecognized financial transaction reported via Kola",
                SupportTicketPriority.Critical,
                "User reported an unrecognized transaction. Immediate review required.",
                cancellationToken);
        }

        if (category == SupportTicketCategory.WalletOrAccount && issueIndex == 1) // "Wallet balance looks wrong"
        {
            return await EscalateToTicketAsync(
                input,
                category,
                "Wallet balance discrepancy reported via Kola",
                SupportTicketPriority.Critical,
                "User reported an unexplained wallet balance discrepancy.",
                cancellationToken);
        }

        // Provide structured resolution advice
        var guidance = GetResolutionGuidance(category, issueIndex);
        var options = new List<string>
        {
            "1. Yes, my issue is resolved",
            "2. No, create a support ticket",
            "3. Speak to human representative"
        };

        return new KolaSessionResponse(
            SessionId: input.SessionId,
            State: KolaSessionState.ResolutionSuggested,
            Category: category,
            BotMessage: guidance + "\n\nDid this resolve your inquiry?",
            Options: options,
            IsEscalated: false,
            Priority: SupportTicketPriority.Normal);
    }

    private async Task<KolaSessionResponse> HandleResolutionStepAsync(
        KolaSessionInput input,
        string message,
        CancellationToken cancellationToken)
    {
        var category = input.Category ?? SupportTicketCategory.Other;

        if (message is "1" or "yes" or "resolved" or "done" or "fixed")
        {
            return new KolaSessionResponse(
                SessionId: input.SessionId,
                State: KolaSessionState.Resolved,
                Category: category,
                BotMessage: "Wonderful! We're glad we could help. Thank you for choosing CebizPay. Have a great day!",
                Options: new List<string> { "Start new inquiry" },
                IsEscalated: false,
                Priority: SupportTicketPriority.Normal);
        }

        if (message is "2" or "no" or "ticket" or "create ticket" or "create a support ticket")
        {
            return await EscalateToTicketAsync(
                input,
                category,
                $"Customer support inquiry for {category}",
                SupportTicketPriority.Normal,
                "Automated resolution did not resolve issue. Ticket created for operator review.",
                cancellationToken);
        }

        if (message is "3" or "human" or "speak" or "representative" or "agent")
        {
            return await EscalateToTicketAsync(
                input,
                category,
                $"Escalated inquiry for {category}",
                SupportTicketPriority.High,
                "Customer requested live operator escalation.",
                cancellationToken);
        }

        return new KolaSessionResponse(
            SessionId: input.SessionId,
            State: KolaSessionState.ResolutionSuggested,
            Category: category,
            BotMessage: "Please select an option:\n1. Issue resolved\n2. Create support ticket\n3. Speak to human representative",
            Options: new List<string>
            {
                "1. Yes, my issue is resolved",
                "2. No, create a support ticket",
                "3. Speak to human representative"
            },
            IsEscalated: false,
            Priority: SupportTicketPriority.Normal);
    }

    private async Task<KolaSessionResponse> EscalateToTicketAsync(
        KolaSessionInput input,
        SupportTicketCategory category,
        string subject,
        SupportTicketPriority priority,
        string reason,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var ticketNumber = _ticketNumberGenerator.GenerateTicketNumber();

        var description = string.IsNullOrWhiteSpace(input.UserMessage)
            ? $"[Kola Triage]: {reason}"
            : $"[Kola Triage]: {reason}\nUser message: {input.UserMessage.Trim()}";

        var ticket = SupportTicket.Create(
            ticketNumber: ticketNumber,
            userId: input.UserId,
            organizationId: input.OrganizationId,
            category: category,
            subject: subject.Length > 200 ? subject[..200] : subject,
            description: description,
            priority: priority,
            now: now);

        ticket.Escalate(now, reason);

        _dbContext.SupportTickets.Add(ticket);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _outboxService?.Write(new SupportTicketCreatedDomainEvent(
            TicketId: ticket.Id,
            TicketNumber: ticket.TicketNumber,
            UserId: ticket.UserId,
            OrganizationId: ticket.OrganizationId,
            Category: ticket.Category,
            Priority: ticket.Priority,
            OccurredOnUtc: now));

        _outboxService?.Write(new SupportTicketEscalatedDomainEvent(
            TicketId: ticket.Id,
            TicketNumber: ticket.TicketNumber,
            UserId: ticket.UserId,
            Priority: ticket.Priority,
            OccurredOnUtc: now));

        await _auditLogService.LogAsync(
            action: AuditActions.SupportTicketEscalated,
            resourceType: AuditResourceTypes.SupportTicket,
            resourceId: ticket.Id.ToString(),
            organizationId: ticket.OrganizationId,
            details: $"Ticket '{ticket.TicketNumber}' created and escalated via Kola triage. Priority: {ticket.Priority}. Reason: {reason}",
            cancellationToken: cancellationToken);

        var responseMessage = priority == SupportTicketPriority.Critical
            ? $"Your issue has been flagged as CRITICAL and escalated to our administrative security and operations team under ticket #{ticket.TicketNumber}. A 12-hour review SLA is in effect. For your safety, do not share your PIN or OTP with anyone."
            : $"Your inquiry has been escalated and support ticket #{ticket.TicketNumber} has been created. Our team will review your case within our 12-hour review SLA.";

        return new KolaSessionResponse(
            SessionId: input.SessionId,
            State: KolaSessionState.TicketCreated,
            Category: category,
            BotMessage: responseMessage,
            Options: new List<string> { "View my tickets", "Start new inquiry" },
            IsEscalated: true,
            Priority: priority,
            CreatedTicketId: ticket.Id,
            CreatedTicketNumber: ticket.TicketNumber);
    }

    private static string GetResolutionGuidance(SupportTicketCategory category, int issueIndex)
    {
        return category switch
        {
            SupportTicketCategory.PaymentOrTransfer => issueIndex switch
            {
                0 => "For failed transfers: funds are automatically reversed to your wallet within 15 minutes. Please check your transaction history.",
                1 => "For pending transfers: NIBSS interbank processing may take up to 2 hours during peak periods. Status will update automatically.",
                2 => "If recipient didn't receive money: Please confirm the recipient account details and retrieve the session reference ID from your transaction receipt.",
                _ => "Please have your transaction reference handy for faster assistance."
            },
            SupportTicketCategory.WalletOrAccount => issueIndex switch
            {
                0 => "If locked out: ensure your network connection is stable and use 'Forgot PIN/Password' on the login screen.",
                2 => "Deposit issues: virtual account deposits typically credit within 60 seconds. Verify the specific dedicated account number used.",
                3 => "Account restrictions: accounts may be temporarily restricted pending KYC verification or risk review.",
                _ => "Please check your account verification status in your profile."
            },
            SupportTicketCategory.KycOrVerification => issueIndex switch
            {
                0 => "KYC verification failure: ensure your NIN or BVN details match your profile name and date of birth exactly.",
                1 => "KYC pending: identity document reviews are processed within 2 to 4 hours during business days.",
                _ => "To update information: go to Profile > Settings > Personal Details."
            },
            SupportTicketCategory.SavingsOrThrift => issueIndex switch
            {
                0 => "Contribution issue: ensure your linked debit wallet has sufficient available balance prior to the deduction cycle.",
                1 => "Payout issue: thrift rotational payouts are credited to your wallet by 12:00 PM on the scheduled cycle payout date.",
                _ => "Check your thrift group rules and current cycle position under the Thrift tab."
            },
            SupportTicketCategory.BusinessOrWorkplace => issueIndex switch
            {
                0 => "Payroll issue: corporate disbursement requires approval by designated workplace finance signatories.",
                1 => "Expense claims: pending reimbursement requests must be signed off by your organization administrator.",
                _ => "Contact your organization portal administrator for workplace privilege changes."
            },
            _ => "We have noted your general inquiry."
        };
    }

    private static bool ContainsAny(string text, string[] terms)
    {
        foreach (var term in terms)
        {
            if (text.Contains(term, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
