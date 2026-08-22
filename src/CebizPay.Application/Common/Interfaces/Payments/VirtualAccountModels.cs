using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Application.Common.Interfaces.Payments;

/// <summary>Request to provision a provider-backed dedicated virtual account.</summary>
public sealed record VirtualAccountCreationRequest(
    string OwnerIdentifier,
    string AccountName,
    string Email,
    string? PhoneNumber,
    Currency Currency,
    string? Bvn = null);

/// <summary>Result from provider virtual account provisioning attempt.</summary>
public sealed record VirtualAccountCreationResult(
    bool Succeeded,
    string? AccountNumber,
    string? AccountName,
    string? BankCode,
    string? BankName,
    string? ProviderReference,
    string? ErrorMessage)
{
    /// <summary>Creates a successful provisioning result.</summary>
    public static VirtualAccountCreationResult Success(
        string accountNumber,
        string accountName,
        string bankCode,
        string bankName,
        string providerReference) =>
        new(true, accountNumber, accountName, bankCode, bankName, providerReference, null);

    /// <summary>Creates a failed provisioning result.</summary>
    public static VirtualAccountCreationResult Failure(string errorMessage) =>
        new(false, null, null, null, null, null, errorMessage);
}

/// <summary>Status query result for a virtual account.</summary>
public sealed record VirtualAccountStatusResult(
    bool IsActive,
    string? ErrorMessage);

/// <summary>Application DTO for returning virtual account details.</summary>
public sealed record VirtualAccountDto(
    Guid Id,
    string? IndividualId,
    Guid? OrganizationId,
    PaymentProvider Provider,
    string AccountNumber,
    string AccountName,
    string BankCode,
    string BankName,
    Currency Currency,
    VirtualAccountStatus Status,
    DateTime CreatedAtUtc);
