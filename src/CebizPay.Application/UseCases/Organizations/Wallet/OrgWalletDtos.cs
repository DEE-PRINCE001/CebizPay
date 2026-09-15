namespace CebizPay.Application.UseCases.Organizations.Wallet;

/// <summary>
/// Overview details of an organization's corporate wallet.
/// </summary>
public sealed record OrgWalletOverviewDto(
    Guid WalletId,
    Guid OrganizationId,
    decimal AvailableBalance,
    decimal LedgerBalance,
    string Currency,
    string Status,
    string? AccountNumber,
    string? AccountName,
    string? BankName,
    string? BankCode);

/// <summary>
/// Data transfer object representing a transaction entry on the organization wallet.
/// </summary>
public sealed record OrgWalletTransactionItemDto(
    Guid Id,
    decimal Amount,
    string Direction,
    string TransactionType,
    string Reference,
    string? Description,
    string? Counterparty,
    string Status,
    DateTime TimestampUtc);
