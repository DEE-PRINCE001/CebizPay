using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Infrastructure.Payments.Monnify.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CebizPay.Infrastructure.Payments.Monnify;

/// <summary>
/// Production-grade HTTP client implementing official Monnify API integration.
/// Implements safe token caching, thread-safe synchronization, source-generated logging, and PII masking.
/// </summary>
public sealed partial class MonnifyClient : IMonnifyClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly MonnifyOptions _options;
    private readonly ILogger<MonnifyClient> _logger;

    private string? _cachedAccessToken;
    private DateTime _tokenExpiresAtUtc = DateTime.MinValue;
    private readonly SemaphoreSlim _authLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes a new instance of <see cref="MonnifyClient"/>.
    /// </summary>
    public MonnifyClient(
        HttpClient httpClient,
        IOptions<MonnifyOptions> options,
        ILogger<MonnifyClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl) && Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            _httpClient.BaseAddress = baseUri;
        }

        if (_options.TimeoutSeconds > 0)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        }
    }

    /// <inheritdoc/>
    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            LogProviderDisabled(_logger);
            return null;
        }

        // Return cached token if valid (with 60-second safety margin)
        if (!string.IsNullOrWhiteSpace(_cachedAccessToken) && DateTime.UtcNow < _tokenExpiresAtUtc.AddSeconds(-60))
        {
            return _cachedAccessToken;
        }

        await _authLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check inside lock
            if (!string.IsNullOrWhiteSpace(_cachedAccessToken) && DateTime.UtcNow < _tokenExpiresAtUtc.AddSeconds(-60))
            {
                return _cachedAccessToken;
            }

            var basicAuthString = $"{_options.ApiKey}:{_options.SecretKey}";
            var basicAuthBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(basicAuthString));

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuthBase64);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var statusCode = (int)response.StatusCode;
                LogAuthFailed(_logger, statusCode);
                return null;
            }

            var authResponse = await response.Content.ReadFromJsonAsync<MonnifyApiResponse<MonnifyAuthResponseBody>>(
                JsonOptions, cancellationToken).ConfigureAwait(false);

            if (authResponse == null || !authResponse.RequestSuccessful || authResponse.ResponseBody == null || string.IsNullOrWhiteSpace(authResponse.ResponseBody.AccessToken))
            {
                LogAuthResponseBodyInvalid(_logger, authResponse?.ResponseMessage ?? "Unknown error");
                return null;
            }

            _cachedAccessToken = authResponse.ResponseBody.AccessToken;
            var expiresIn = authResponse.ResponseBody.ExpiresIn > 0 ? authResponse.ResponseBody.ExpiresIn : 3600;
            _tokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn);

            LogAuthSuccess(_logger, expiresIn);
            return _cachedAccessToken;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogAuthException(_logger, ex);
            return null;
        }
        finally
        {
            _authLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<MonnifyApiResponse<MonnifyCreateReservedAccountResponseBody>?> CreateReservedAccountAsync(
        MonnifyCreateReservedAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            LogTokenUnavailable(_logger, "CreateReservedAccount");
            return null;
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v2/bank-transfer/reserved-accounts")
            {
                Content = JsonContent.Create(request, options: JsonOptions)
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var result = await response.Content.ReadFromJsonAsync<MonnifyApiResponse<MonnifyCreateReservedAccountResponseBody>>(
                JsonOptions, cancellationToken).ConfigureAwait(false);

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogApiCallException(_logger, "CreateReservedAccount", ex);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<MonnifyApiResponse<object>?> DeactivateReservedAccountAsync(
        string accountReference,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accountReference))
            throw new ArgumentException("AccountReference is required.", nameof(accountReference));

        var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            LogTokenUnavailable(_logger, "DeactivateReservedAccount");
            return null;
        }

        try
        {
            var uri = $"/api/v1/bank-transfer/reserved-accounts/{Uri.EscapeDataString(accountReference)}";
            using var httpRequest = new HttpRequestMessage(HttpMethod.Delete, uri);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var result = await response.Content.ReadFromJsonAsync<MonnifyApiResponse<object>>(
                JsonOptions, cancellationToken).ConfigureAwait(false);

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogApiCallException(_logger, "DeactivateReservedAccount", ex);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<MonnifyApiResponse<MonnifyTransactionResponseBody>?> GetTransactionDetailsAsync(
        string transactionReference,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transactionReference))
            throw new ArgumentException("TransactionReference is required.", nameof(transactionReference));

        var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            LogTokenUnavailable(_logger, "GetTransactionDetails");
            return null;
        }

        try
        {
            var uri = $"/api/v2/transactions/{Uri.EscapeDataString(transactionReference)}";
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, uri);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var result = await response.Content.ReadFromJsonAsync<MonnifyApiResponse<MonnifyTransactionResponseBody>>(
                JsonOptions, cancellationToken).ConfigureAwait(false);

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogApiCallException(_logger, "GetTransactionDetails", ex);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<PaymentProviderResult> InitiateTransferAsync(
        string destinationBankCode,
        string destinationAccountNumber,
        decimal amount,
        string currency,
        string reference,
        string narration,
        string? destinationAccountName = null,
        string? sourceAccountNumber = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destinationBankCode))
            throw new ArgumentException("DestinationBankCode is required.", nameof(destinationBankCode));
        if (string.IsNullOrWhiteSpace(destinationAccountNumber))
            throw new ArgumentException("DestinationAccountNumber is required.", nameof(destinationAccountNumber));
        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("Reference is required.", nameof(reference));

        var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            LogTokenUnavailable(_logger, "InitiateTransfer");
            return PaymentProviderResult.TechnicalFailure("AUTH_FAILED", "Monnify authentication failed.");
        }

        var sourceAcc = !string.IsNullOrWhiteSpace(sourceAccountNumber)
            ? sourceAccountNumber.Trim()
            : (!string.IsNullOrWhiteSpace(_options.SourceAccountNumber) ? _options.SourceAccountNumber.Trim() : _options.ContractCode);

        var requestPayload = new MonnifySingleTransferRequest
        {
            Amount = amount,
            Reference = reference.Trim(),
            Narration = string.IsNullOrWhiteSpace(narration) ? "CebizPay Transfer" : narration.Trim(),
            DestinationBankCode = destinationBankCode.Trim(),
            DestinationAccountNumber = destinationAccountNumber.Trim(),
            DestinationAccountName = string.IsNullOrWhiteSpace(destinationAccountName) ? null : destinationAccountName.Trim(),
            Currency = string.IsNullOrWhiteSpace(currency) ? "NGN" : currency.Trim().ToUpperInvariant(),
            SourceAccountNumber = sourceAcc,
            Async = false
        };

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v2/disbursements/single")
            {
                Content = JsonContent.Create(requestPayload, options: JsonOptions)
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var statusCode = (int)response.StatusCode;

            var responseBody = await response.Content.ReadFromJsonAsync<MonnifyApiResponse<MonnifySingleTransferResponseBody>>(
                JsonOptions, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode && responseBody != null && responseBody.RequestSuccessful && responseBody.ResponseBody != null)
            {
                var body = responseBody.ResponseBody;
                var status = (body.Status ?? string.Empty).ToUpperInvariant();
                var txRef = body.TransactionReference ?? body.Reference ?? reference;

                var safeMeta = JsonSerializer.Serialize(new
                {
                    monnify_reference = txRef,
                    status = status,
                    fee = body.Fee,
                    destination_account_name = body.DestinationAccountName ?? destinationAccountName,
                    destination_bank_code = body.DestinationBankCode
                });

                if (status is "SUCCESS" or "SUCCESSFUL" or "PAID")
                {
                    return PaymentProviderResult.Success(txRef, safeMeta);
                }

                if (status is "PENDING" or "IN_PROGRESS" or "START" or "AWAITING_AUTHORIZATION" or "PROCESSING" or "QUEUED" or "AWAITING_OTP")
                {
                    return PaymentProviderResult.Unknown($"Monnify disbursement is in progress (status: '{status}').", safeMeta);
                }

                if (status is "FAILED" or "REVERSED" or "REJECTED" or "EXPIRED" or "CANCELLED")
                {
                    return PaymentProviderResult.BusinessFailure(
                        responseBody.ResponseCode ?? "DISBURSEMENT_FAILED",
                        body.TransactionDescription ?? responseBody.ResponseMessage ?? "Disbursement failed.",
                        safeMeta);
                }

                return PaymentProviderResult.Unknown($"Monnify disbursement returned status '{status}'.", safeMeta);
            }

            if (statusCode >= 500)
            {
                return PaymentProviderResult.TechnicalFailure(
                    $"HTTP_{statusCode}",
                    responseBody?.ResponseMessage ?? "Monnify server error during disbursement.");
            }

            return PaymentProviderResult.BusinessFailure(
                responseBody?.ResponseCode ?? $"HTTP_{statusCode}",
                responseBody?.ResponseMessage ?? "Monnify disbursement request was rejected.");
        }
        catch (OperationCanceledException)
        {
            return PaymentProviderResult.Unknown("Disbursement timed out during Monnify API call.");
        }
        catch (Exception ex)
        {
            LogApiCallException(_logger, "InitiateTransfer", ex);
            return PaymentProviderResult.Unknown($"Exception during Monnify disbursement: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<PaymentProviderResult> GetTransferStatusAsync(
        string reference,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("Reference is required.", nameof(reference));

        var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            LogTokenUnavailable(_logger, "GetTransferStatus");
            return PaymentProviderResult.TechnicalFailure("AUTH_FAILED", "Monnify authentication failed.");
        }

        try
        {
            var uri = $"/api/v2/disbursements/single/summary?reference={Uri.EscapeDataString(reference.Trim())}";
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, uri);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var statusCode = (int)response.StatusCode;

            var responseBody = await response.Content.ReadFromJsonAsync<MonnifyApiResponse<MonnifyDisbursementSummaryResponseBody>>(
                JsonOptions, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode && responseBody != null && responseBody.RequestSuccessful && responseBody.ResponseBody != null)
            {
                var body = responseBody.ResponseBody;
                var status = (body.Status ?? string.Empty).ToUpperInvariant();
                var txRef = body.TransactionReference ?? body.Reference ?? reference;

                var safeMeta = JsonSerializer.Serialize(new
                {
                    monnify_reference = txRef,
                    status = status,
                    fee = body.Fee,
                    destination_account_name = body.DestinationAccountName
                });

                if (status is "SUCCESS" or "SUCCESSFUL")
                {
                    return PaymentProviderResult.Success(txRef, safeMeta);
                }

                if (status is "FAILED" or "REVERSED")
                {
                    return PaymentProviderResult.BusinessFailure(
                        "DISBURSEMENT_FAILED",
                        body.Message ?? responseBody.ResponseMessage ?? "Disbursement failed.",
                        safeMeta);
                }

                return PaymentProviderResult.Unknown($"Disbursement status is '{status}'.", safeMeta);
            }

            if (statusCode == 404)
            {
                return PaymentProviderResult.Unknown("Disbursement reference not found on Monnify.");
            }

            if (statusCode >= 500)
            {
                return PaymentProviderResult.TechnicalFailure(
                    $"HTTP_{statusCode}",
                    responseBody?.ResponseMessage ?? "Monnify server error querying disbursement status.");
            }

            return PaymentProviderResult.BusinessFailure(
                responseBody?.ResponseCode ?? $"HTTP_{statusCode}",
                responseBody?.ResponseMessage ?? "Disbursement status query failed.");
        }
        catch (OperationCanceledException)
        {
            return PaymentProviderResult.Unknown("Disbursement status query timed out.");
        }
        catch (Exception ex)
        {
            LogApiCallException(_logger, "GetTransferStatus", ex);
            return PaymentProviderResult.Unknown($"Exception querying Monnify disbursement: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<CebizPay.Application.Common.Interfaces.Finance.BankAccountResolutionResult> ResolveAccountAsync(
        string bankCode,
        string accountNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bankCode) || string.IsNullOrWhiteSpace(accountNumber))
        {
            return new CebizPay.Application.Common.Interfaces.Finance.BankAccountResolutionResult(
                Succeeded: false,
                AccountName: null,
                BankCode: bankCode,
                AccountNumber: accountNumber,
                ErrorMessage: "Bank code and account number are required.");
        }

        var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            return new CebizPay.Application.Common.Interfaces.Finance.BankAccountResolutionResult(
                Succeeded: false,
                AccountName: null,
                BankCode: bankCode,
                AccountNumber: accountNumber,
                ErrorMessage: "Monnify authentication failed.");
        }

        try
        {
            var cleanBankCode = bankCode.Trim();
            var cleanAccountNumber = accountNumber.Trim();
            var uri = $"/api/v1/disbursements/account/validate?accountNumber={Uri.EscapeDataString(cleanAccountNumber)}&bankCode={Uri.EscapeDataString(cleanBankCode)}";

            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, uri);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadFromJsonAsync<MonnifyApiResponse<MonnifyAccountValidationResponseBody>>(
                JsonOptions, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode && responseBody != null && responseBody.RequestSuccessful && responseBody.ResponseBody != null && !string.IsNullOrWhiteSpace(responseBody.ResponseBody.AccountName))
            {
                return new CebizPay.Application.Common.Interfaces.Finance.BankAccountResolutionResult(
                    Succeeded: true,
                    AccountName: responseBody.ResponseBody.AccountName.Trim(),
                    BankCode: cleanBankCode,
                    AccountNumber: cleanAccountNumber,
                    ErrorMessage: null);
            }

            return new CebizPay.Application.Common.Interfaces.Finance.BankAccountResolutionResult(
                Succeeded: false,
                AccountName: null,
                BankCode: cleanBankCode,
                AccountNumber: cleanAccountNumber,
                ErrorMessage: responseBody?.ResponseMessage ?? "Monnify account validation failed.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogApiCallException(_logger, "ResolveAccount", ex);
            return new CebizPay.Application.Common.Interfaces.Finance.BankAccountResolutionResult(
                Succeeded: false,
                AccountName: null,
                BankCode: bankCode,
                AccountNumber: accountNumber,
                ErrorMessage: $"Exception resolving account with Monnify: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _authLock.Dispose();
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Monnify provider is disabled in configuration.")]
    private static partial void LogProviderDisabled(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Monnify OAuth2 authentication failed with status code {StatusCode}.")]
    private static partial void LogAuthFailed(ILogger logger, int statusCode);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Monnify OAuth2 response payload invalid: {Message}")]
    private static partial void LogAuthResponseBodyInvalid(ILogger logger, string message);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Monnify OAuth2 authentication succeeded. Token cached for {ExpiresIn}s.")]
    private static partial void LogAuthSuccess(ILogger logger, int expiresIn);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Exception occurred during Monnify OAuth2 authentication.")]
    private static partial void LogAuthException(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "Monnify access token unavailable for operation '{Operation}'.")]
    private static partial void LogTokenUnavailable(ILogger logger, string operation);

    [LoggerMessage(EventId = 7, Level = LogLevel.Error, Message = "Exception occurred during Monnify API call '{Operation}'.")]
    private static partial void LogApiCallException(ILogger logger, string operation, Exception ex);
}
