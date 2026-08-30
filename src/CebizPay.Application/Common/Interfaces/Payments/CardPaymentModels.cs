using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;

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

/// <summary>Tokenized card metadata extracted from successful authentication or verification.</summary>
public sealed record CardTokenDetails(
    string Token,
    string Last4,
    string Brand,
    string? ExpiryMonth,
    string? ExpiryYear,
    string? CardHolderName,
    bool Reusable = true);

/// <summary>Request to charge an existing tokenized saved card.</summary>
public sealed record CardSavedChargeRequest(
    string ProviderToken,
    decimal Amount,
    Currency Currency,
    string Email,
    string Reference,
    string? CustomerName = null);

/// <summary>Result from charging a tokenized saved card.</summary>
public sealed record CardChargeResult(
    PaymentProviderResultStatus Status,
    string? ProviderReference,
    string? FailureCode,
    string? FailureReason,
    string? RawMetadata,
    CardTokenDetails? TokenDetails = null)
{
    /// <summary>Creates a successful charge result.</summary>
    public static CardChargeResult Success(string providerReference, string? rawMetadata = null, CardTokenDetails? tokenDetails = null) =>
        new(PaymentProviderResultStatus.Success, providerReference, null, null, rawMetadata, tokenDetails);

    /// <summary>Creates a business failure charge result.</summary>
    public static CardChargeResult BusinessFailure(string code, string reason, string? rawMetadata = null) =>
        new(PaymentProviderResultStatus.BusinessFailure, null, code, reason, rawMetadata, null);

    /// <summary>Creates a technical failure charge result.</summary>
    public static CardChargeResult TechnicalFailure(string code, string reason, string? rawMetadata = null) =>
        new(PaymentProviderResultStatus.TechnicalFailure, null, code, reason, rawMetadata, null);

    /// <summary>Creates an unknown outcome charge result.</summary>
    public static CardChargeResult Unknown(string reason, string? rawMetadata = null) =>
        new(PaymentProviderResultStatus.Unknown, null, "UNKNOWN", reason, rawMetadata, null);
}

/// <summary>Request to execute a card payment refund on the external gateway.</summary>
public sealed record CardRefundRequest(
    string ProviderTransactionReference,
    decimal Amount,
    Currency Currency,
    string RefundReference,
    string Reason);

/// <summary>Result from provider refund execution.</summary>
public sealed record CardRefundResult(
    bool Succeeded,
    string? ProviderRefundReference,
    string? Status,
    string? ErrorMessage)
{
    /// <summary>Creates a successful refund result.</summary>
    public static CardRefundResult Success(string? providerRefundReference, string status = "processed") =>
        new(true, providerRefundReference, status, null);

    /// <summary>Creates a failed refund result.</summary>
    public static CardRefundResult Failure(string errorMessage) =>
        new(false, null, "failed", errorMessage);
}

/// <summary>Request to initialize card verification (zero-auth or nominal micro-charge).</summary>
public sealed record CardVerificationRequest(
    string Email,
    string Reference,
    string CallbackUrl,
    decimal Amount = 0m,
    Currency Currency = Currency.NGN,
    string? CustomerName = null);

/// <summary>Result from provider card verification initialization.</summary>
public sealed record CardVerificationResult(
    bool Succeeded,
    string? AuthorizationUrl,
    string? ProviderReference,
    string? ErrorMessage)
{
    /// <summary>Creates a successful card verification initialization result.</summary>
    public static CardVerificationResult Success(string authorizationUrl, string? providerReference) =>
        new(true, authorizationUrl, providerReference, null);

    /// <summary>Creates a failed card verification result.</summary>
    public static CardVerificationResult Failure(string errorMessage) =>
        new(false, null, null, errorMessage);
}

/// <summary>Application response for initializing card funding.</summary>
public sealed record CardFundingInitializationResponse(
    Guid FundingTransactionId,
    string Reference,
    string AuthorizationUrl,
    string Provider);

/// <summary>Application response for charging a tokenized saved card.</summary>
public sealed record ChargeSavedCardResponseDto(
    Guid FundingTransactionId,
    string Reference,
    string Status,
    decimal GrossAmount,
    decimal FeeAmount,
    decimal NetCreditedAmount,
    string Currency,
    string Provider);

/// <summary>DTO representation of a tokenized saved card.</summary>
public sealed record SavedCardResponseDto(
    Guid Id,
    string UserId,
    Guid WalletId,
    string Provider,
    string Last4,
    string Brand,
    string? ExpiryMonth,
    string? ExpiryYear,
    string? CardHolderName,
    string Status,
    bool IsDefault,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>DTO representation of a card refund.</summary>
public sealed record CardRefundResponseDto(
    Guid Id,
    Guid FundingTransactionId,
    Guid WalletId,
    string Provider,
    string RefundReference,
    string? ProviderRefundReference,
    decimal Amount,
    string Currency,
    string Status,
    string Reason,
    Guid? LedgerTransactionId,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc);

/// <summary>DTO representation of a card verification operation.</summary>
public sealed record CardVerificationResponseDto(
    Guid Id,
    string UserId,
    Guid WalletId,
    string Provider,
    string Reference,
    string? ProviderReference,
    Guid? SavedCardId,
    decimal Amount,
    string Currency,
    string Status,
    string? AuthorizationUrl,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc);
