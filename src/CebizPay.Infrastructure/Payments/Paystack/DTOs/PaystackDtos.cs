using System.Text.Json.Serialization;

namespace CebizPay.Infrastructure.Payments.Paystack.DTOs;

internal sealed record PaystackAccountResolveResponse(
    [property: JsonPropertyName("status")] bool Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("data")] PaystackAccountResolveData? Data);

internal sealed record PaystackAccountResolveData(
    [property: JsonPropertyName("account_number")] string? AccountNumber,
    [property: JsonPropertyName("account_name")] string? AccountName,
    [property: JsonPropertyName("bank_id")] int? BankId);

internal sealed record PaystackCreateRecipientRequest(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("account_number")] string AccountNumber,
    [property: JsonPropertyName("bank_code")] string BankCode,
    [property: JsonPropertyName("currency")] string Currency);

internal sealed record PaystackCreateRecipientResponse(
    [property: JsonPropertyName("status")] bool Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("data")] PaystackRecipientData? Data);

internal sealed record PaystackRecipientData(
    [property: JsonPropertyName("recipient_code")] string? RecipientCode,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("active")] bool? Active);

internal sealed record PaystackTransferRequest(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("reference")] string Reference,
    [property: JsonPropertyName("recipient")] string Recipient,
    [property: JsonPropertyName("reason")] string Reason);

internal sealed record PaystackTransferResponse(
    [property: JsonPropertyName("status")] bool Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("data")] PaystackTransferData? Data);

internal sealed record PaystackTransferData(
    [property: JsonPropertyName("reference")] string? Reference,
    [property: JsonPropertyName("transfer_code")] string? TransferCode,
    [property: JsonPropertyName("amount")] decimal? Amount,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("id")] long? Id,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("recipient")] long? RecipientId);

internal sealed record PaystackVerifyTransferResponse(
    [property: JsonPropertyName("status")] bool Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("data")] PaystackVerifyTransferData? Data);

internal sealed record PaystackVerifyTransferData(
    [property: JsonPropertyName("reference")] string? Reference,
    [property: JsonPropertyName("transfer_code")] string? TransferCode,
    [property: JsonPropertyName("amount")] decimal? Amount,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("failures")] object? Failures);

// --- Phase 3F Dedicated Virtual Accounts & Card Transactions ---

internal sealed record PaystackCreateCustomerRequest(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("first_name")] string? FirstName,
    [property: JsonPropertyName("last_name")] string? LastName,
    [property: JsonPropertyName("phone")] string? Phone);

internal sealed record PaystackCreateCustomerResponse(
    [property: JsonPropertyName("status")] bool Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("data")] PaystackCustomerData? Data);

internal sealed record PaystackCustomerData(
    [property: JsonPropertyName("id")] long? Id,
    [property: JsonPropertyName("customer_code")] string? CustomerCode,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("first_name")] string? FirstName,
    [property: JsonPropertyName("last_name")] string? LastName);

internal sealed record PaystackCreateDedicatedAccountRequest(
    [property: JsonPropertyName("customer")] string Customer,
    [property: JsonPropertyName("preferred_bank")] string? PreferredBank = null);

internal sealed record PaystackCreateDedicatedAccountResponse(
    [property: JsonPropertyName("status")] bool Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("data")] PaystackDedicatedAccountData? Data);

internal sealed record PaystackDedicatedAccountData(
    [property: JsonPropertyName("id")] long? Id,
    [property: JsonPropertyName("account_number")] string? AccountNumber,
    [property: JsonPropertyName("account_name")] string? AccountName,
    [property: JsonPropertyName("bank")] PaystackDedicatedAccountBank? Bank,
    [property: JsonPropertyName("active")] bool? Active);

internal sealed record PaystackDedicatedAccountBank(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("id")] int? Id,
    [property: JsonPropertyName("slug")] string? Slug);

internal sealed record PaystackInitializeTransactionRequest(
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("reference")] string Reference,
    [property: JsonPropertyName("callback_url")] string CallbackUrl);

internal sealed record PaystackInitializeTransactionResponse(
    [property: JsonPropertyName("status")] bool Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("data")] PaystackInitializeTransactionData? Data);

internal sealed record PaystackInitializeTransactionData(
    [property: JsonPropertyName("authorization_url")] string? AuthorizationUrl,
    [property: JsonPropertyName("access_code")] string? AccessCode,
    [property: JsonPropertyName("reference")] string? Reference);

internal sealed record PaystackVerifyTransactionResponse(
    [property: JsonPropertyName("status")] bool Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("data")] PaystackVerifyTransactionData? Data);

internal sealed record PaystackVerifyTransactionData(
    [property: JsonPropertyName("id")] long? Id,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("reference")] string? Reference,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("gateway_response")] string? GatewayResponse,
    [property: JsonPropertyName("channel")] string? Channel);
