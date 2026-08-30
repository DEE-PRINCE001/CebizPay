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

internal sealed record PaystackAuthorizationData(
    [property: JsonPropertyName("authorization_code")] string? AuthorizationCode,
    [property: JsonPropertyName("bin")] string? Bin,
    [property: JsonPropertyName("last4")] string? Last4,
    [property: JsonPropertyName("exp_month")] string? ExpMonth,
    [property: JsonPropertyName("exp_year")] string? ExpYear,
    [property: JsonPropertyName("channel")] string? Channel,
    [property: JsonPropertyName("card_type")] string? CardType,
    [property: JsonPropertyName("bank")] string? Bank,
    [property: JsonPropertyName("brand")] string? Brand,
    [property: JsonPropertyName("reusable")] bool? Reusable);

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
    [property: JsonPropertyName("channel")] string? Channel,
    [property: JsonPropertyName("authorization")] PaystackAuthorizationData? Authorization = null,
    [property: JsonPropertyName("customer")] PaystackCustomerData? Customer = null);

// --- Batch 3 Tokenized Charges & Refunds ---

internal sealed record PaystackChargeAuthorizationRequest(
    [property: JsonPropertyName("authorization_code")] string AuthorizationCode,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("reference")] string Reference,
    [property: JsonPropertyName("currency")] string Currency);

internal sealed record PaystackChargeAuthorizationResponse(
    [property: JsonPropertyName("status")] bool Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("data")] PaystackVerifyTransactionData? Data);

internal sealed record PaystackRefundRequest(
    [property: JsonPropertyName("transaction")] string Transaction,
    [property: JsonPropertyName("amount")] decimal? Amount = null,
    [property: JsonPropertyName("currency")] string? Currency = null,
    [property: JsonPropertyName("merchant_note")] string? MerchantNote = null);

internal sealed record PaystackRefundResponse(
    [property: JsonPropertyName("status")] bool Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("data")] PaystackRefundData? Data);

internal sealed record PaystackRefundData(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("transaction_reference")] string? TransactionReference,
    [property: JsonPropertyName("amount")] decimal? Amount);
