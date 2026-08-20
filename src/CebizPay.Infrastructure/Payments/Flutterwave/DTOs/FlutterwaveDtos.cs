using System.Text.Json.Serialization;

namespace CebizPay.Infrastructure.Payments.Flutterwave.DTOs;

internal sealed record FlutterwaveAccountResolveRequest(
    [property: JsonPropertyName("account_number")] string AccountNumber,
    [property: JsonPropertyName("account_bank")] string AccountBank);

internal sealed record FlutterwaveAccountResolveResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("data")] FlutterwaveAccountResolveData? Data);

internal sealed record FlutterwaveAccountResolveData(
    [property: JsonPropertyName("account_number")] string? AccountNumber,
    [property: JsonPropertyName("account_name")] string? AccountName);

internal sealed record FlutterwaveTransferRequest(
    [property: JsonPropertyName("account_bank")] string AccountBank,
    [property: JsonPropertyName("account_number")] string AccountNumber,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("narration")] string Narration,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("reference")] string Reference,
    [property: JsonPropertyName("callback_url")] string? CallbackUrl = null,
    [property: JsonPropertyName("debit_currency")] string? DebitCurrency = null);

internal sealed record FlutterwaveTransferResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("data")] FlutterwaveTransferData? Data);

internal sealed record FlutterwaveTransferData(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("account_number")] string? AccountNumber,
    [property: JsonPropertyName("bank_code")] string? BankCode,
    [property: JsonPropertyName("full_name")] string? FullName,
    [property: JsonPropertyName("created_at")] string? CreatedAt,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("fee")] decimal? Fee,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("reference")] string? Reference,
    [property: JsonPropertyName("narration")] string? Narration,
    [property: JsonPropertyName("complete_message")] string? CompleteMessage,
    [property: JsonPropertyName("bank_name")] string? BankName);

internal sealed record FlutterwaveTransferStatusResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("data")] FlutterwaveTransferData? Data);
