using CebizPay.Infrastructure.Payments.Monnify.Models;

namespace CebizPay.Infrastructure.Payments.Monnify;

/// <summary>
/// Infrastructure HTTP client interface for interacting with the official Monnify REST API.
/// </summary>
public interface IMonnifyClient
{
    /// <summary>
    /// Obtains an authenticated OAuth2 access token, using safe in-memory caching.
    /// </summary>
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a dedicated reserved virtual account on Monnify.
    /// </summary>
    Task<MonnifyApiResponse<MonnifyCreateReservedAccountResponseBody>?> CreateReservedAccountAsync(
        MonnifyCreateReservedAccountRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates a reserved virtual account on Monnify.
    /// </summary>
    Task<MonnifyApiResponse<object>?> DeactivateReservedAccountAsync(
        string accountReference,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries the status and details of a transaction on Monnify.
    /// </summary>
    Task<MonnifyApiResponse<MonnifyTransactionResponseBody>?> GetTransactionDetailsAsync(
        string transactionReference,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Initiates a single outbound bank transfer disbursement via Monnify.
    /// </summary>
    Task<CebizPay.Application.Common.Interfaces.Payments.PaymentProviderResult> InitiateTransferAsync(
        string destinationBankCode,
        string destinationAccountNumber,
        decimal amount,
        string currency,
        string reference,
        string narration,
        string? destinationAccountName = null,
        string? sourceAccountNumber = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries the status of an outbound bank transfer disbursement on Monnify.
    /// </summary>
    Task<CebizPay.Application.Common.Interfaces.Payments.PaymentProviderResult> GetTransferStatusAsync(
        string reference,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and resolves destination bank account details on Monnify.
    /// </summary>
    Task<CebizPay.Application.Common.Interfaces.Finance.BankAccountResolutionResult> ResolveAccountAsync(
        string bankCode,
        string accountNumber,
        CancellationToken cancellationToken = default);
}
