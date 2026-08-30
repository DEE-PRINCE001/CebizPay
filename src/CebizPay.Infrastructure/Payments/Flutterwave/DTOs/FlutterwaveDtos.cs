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

// --- Phase 3F Virtual Accounts & Card Payments ---

internal sealed record FlutterwaveVirtualAccountCreateRequest(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("is_permanent")] bool IsPermanent,
    [property: JsonPropertyName("bvn")] string? Bvn,
    [property: JsonPropertyName("tx_ref")] string TxRef,
    [property: JsonPropertyName("phonenumber")] string? Phonenumber,
    [property: JsonPropertyName("firstname")] string? FirstName,
    [property: JsonPropertyName("lastname")] string? LastName,
    [property: JsonPropertyName("narration")] string Narration);

internal sealed record FlutterwaveVirtualAccountResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("data")] FlutterwaveVirtualAccountData? Data);

internal sealed record FlutterwaveVirtualAccountData(
    [property: JsonPropertyName("order_ref")] string? OrderRef,
    [property: JsonPropertyName("account_number")] string? AccountNumber,
    [property: JsonPropertyName("bank_name")] string? BankName,
    [property: JsonPropertyName("flw_ref")] string? FlwRef,
    [property: JsonPropertyName("account_status")] string? AccountStatus,
    [property: JsonPropertyName("response_code")] string? ResponseCode,
    [property: JsonPropertyName("response_message")] string? ResponseMessage);

internal sealed record FlutterwaveInitializePaymentRequest(
    [property: JsonPropertyName("tx_ref")] string TxRef,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("redirect_url")] string RedirectUrl,
    [property: JsonPropertyName("customer")] FlutterwaveCustomer Customer,
    [property: JsonPropertyName("customizations")] FlutterwaveCustomizations? Customizations = null);

internal sealed record FlutterwaveCustomer(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("phonenumber")] string? Phonenumber = null);

internal sealed record FlutterwaveCustomizations(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description = null);

internal sealed record FlutterwaveInitializePaymentResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("data")] FlutterwaveInitializePaymentData? Data);

internal sealed record FlutterwaveInitializePaymentData(
    [property: JsonPropertyName("link")] string? Link);

internal sealed record FlutterwaveCardDetails(
    [property: JsonPropertyName("first_6digits")] string? First6Digits,
    [property: JsonPropertyName("last_4digits")] string? Last4Digits,
    [property: JsonPropertyName("issuer")] string? Issuer,
    [property: JsonPropertyName("country")] string? Country,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("token")] string? Token,
    [property: JsonPropertyName("expiry")] string? Expiry);

internal sealed record FlutterwaveVerifyTransactionResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("data")] FlutterwaveVerifyTransactionData? Data);

internal sealed record FlutterwaveVerifyTransactionData(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("tx_ref")] string? TxRef,
    [property: JsonPropertyName("flw_ref")] string? FlwRef,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("payment_type")] string? PaymentType,
    [property: JsonPropertyName("app_fee")] decimal? AppFee,
    [property: JsonPropertyName("processor_response")] string? ProcessorResponse,
    [property: JsonPropertyName("card")] FlutterwaveCardDetails? Card = null,
    [property: JsonPropertyName("customer")] FlutterwaveCustomer? Customer = null);

// --- Batch 3 Tokenized Charges & Refunds ---

internal sealed record FlutterwaveTokenizedChargeRequest(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("country")] string Country,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("first_name")] string? FirstName,
    [property: JsonPropertyName("last_name")] string? LastName,
    [property: JsonPropertyName("tx_ref")] string TxRef,
    [property: JsonPropertyName("narration")] string? Narration = null);

internal sealed record FlutterwaveTokenizedChargeResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("data")] FlutterwaveTokenizedChargeData? Data);

internal sealed record FlutterwaveTokenizedChargeData(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("tx_ref")] string? TxRef,
    [property: JsonPropertyName("flw_ref")] string? FlwRef,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("processor_response")] string? ProcessorResponse,
    [property: JsonPropertyName("card")] FlutterwaveCardDetails? Card = null);

internal sealed record FlutterwaveRefundRequest(
    [property: JsonPropertyName("amount")] decimal? Amount = null,
    [property: JsonPropertyName("comments")] string? Comments = null);

internal sealed record FlutterwaveRefundResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("data")] FlutterwaveRefundData? Data);

internal sealed record FlutterwaveRefundData(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("flw_ref")] string? FlwRef,
    [property: JsonPropertyName("amount_refunded")] decimal? AmountRefunded);
