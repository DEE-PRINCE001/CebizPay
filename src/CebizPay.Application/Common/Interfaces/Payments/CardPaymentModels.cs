using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Application.Common.Interfaces.Payments;

/// <summary>Request to initialize a hosted/inline card payment session.</summary>
public sealed record CardPaymentInitializationRequest(
    decimal Amount,
    Currency Currency,
    string Email,
    string Reference,
    string CallbackUrl,
    string? CustomerName = null);

/// <summary>Result from provider card payment initialization.</summary>
public sealed record CardPaymentInitializationResult(
    bool Succeeded,
    string? AuthorizationUrl,
    string? AccessCode,
    string? Reference,
    string? ErrorMessage)
{
    /// <summary>Creates a successful initialization result.</summary>
    public static CardPaymentInitializationResult Success(
        string authorizationUrl,
        string? accessCode,
        string reference) =>
        new(true, authorizationUrl, accessCode, reference, null);

    /// <summary>Creates a failed initialization result.</summary>
    public static CardPaymentInitializationResult Failure(string errorMessage) =>
        new(false, null, null, null, errorMessage);
}

/// <summary>Application response for initializing card funding.</summary>
public sealed record CardFundingInitializationResponse(
    Guid FundingTransactionId,
    string Reference,
    string AuthorizationUrl,
    string Provider);
