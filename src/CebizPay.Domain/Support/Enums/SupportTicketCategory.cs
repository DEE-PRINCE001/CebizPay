namespace CebizPay.Domain.Support.Enums;

/// <summary>
/// Authoritative categories for customer support tickets and Kola chatbot triage.
/// </summary>
public enum SupportTicketCategory
{
    /// <summary>Payment transfers, deposit verifications, and transaction failures.</summary>
    PaymentOrTransfer = 1,

    /// <summary>Wallet balances, account restrictions, authentication, and access.</summary>
    WalletOrAccount = 2,

    /// <summary>Individual KYC compliance, document submissions, and verification status.</summary>
    KycOrVerification = 3,

    /// <summary>Savings plans, thrift cycles, missed contributions, and payouts.</summary>
    SavingsOrThrift = 4,

    /// <summary>Workforce payroll, corporate expenses, invoicing, and business portal issues.</summary>
    BusinessOrWorkplace = 5,

    /// <summary>General inquiries and non-categorized support matters.</summary>
    Other = 6
}
