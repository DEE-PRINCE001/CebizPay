using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Application.Common.Interfaces.Finance;

/// <summary>
/// Data transfer representation of an external funding account attached to a wallet.
/// </summary>
public sealed record ExternalFundingAccountDto(
    Guid Id,
    Guid WalletId,
    PaymentProvider Provider,
    string ProviderName,
    string? ProviderCustomerReference,
    string? ProviderAccountReference,
    string AccountNumber,
    string AccountName,
    string BankCode,
    string BankName,
    Currency Currency,
    string CurrencyCode,
    ExternalFundingAccountStatus Status,
    string StatusName,
    bool IsPrimary,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
