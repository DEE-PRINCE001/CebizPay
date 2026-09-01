using System.Text.Json.Serialization;

namespace CebizPay.Infrastructure.Payments.Monnify.Models;

/// <summary>
/// Generic envelope for Monnify API responses.
/// </summary>
/// <typeparam name="T">Payload response body type.</typeparam>
public sealed class MonnifyApiResponse<T>
{
    /// <summary>Indicates whether the API request succeeded.</summary>
    [JsonPropertyName("requestSuccessful")]
    public bool RequestSuccessful { get; set; }

    /// <summary>Descriptive message returned by the provider.</summary>
    [JsonPropertyName("responseMessage")]
    public string? ResponseMessage { get; set; }

    /// <summary>Monnify response code.</summary>
    [JsonPropertyName("responseCode")]
    public string? ResponseCode { get; set; }

    /// <summary>Typed response body payload.</summary>
    [JsonPropertyName("responseBody")]
    public T? ResponseBody { get; set; }
}

/// <summary>
/// Monnify OAuth2 authentication response payload.
/// </summary>
public sealed class MonnifyAuthResponseBody
{
    /// <summary>Bearer access token.</summary>
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Token validity lifespan in seconds.</summary>
    [JsonPropertyName("expiresIn")]
    public int ExpiresIn { get; set; } = 3600;
}

/// <summary>
/// Monnify reserved account creation request payload.
/// </summary>
public sealed class MonnifyCreateReservedAccountRequest
{
    /// <summary>Unique customer/wallet account reference.</summary>
    [JsonPropertyName("accountReference")]
    public string AccountReference { get; set; } = string.Empty;

    /// <summary>Account name to display on the virtual account.</summary>
    [JsonPropertyName("accountName")]
    public string AccountName { get; set; } = string.Empty;

    /// <summary>Currency code (e.g. NGN).</summary>
    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; set; } = "NGN";

    /// <summary>Merchant Monnify contract code.</summary>
    [JsonPropertyName("contractCode")]
    public string ContractCode { get; set; } = string.Empty;

    /// <summary>Customer email address.</summary>
    [JsonPropertyName("customerEmail")]
    public string CustomerEmail { get; set; } = string.Empty;

    /// <summary>Customer full name.</summary>
    [JsonPropertyName("customerName")]
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>Customer Bank Verification Number (BVN) if available.</summary>
    [JsonPropertyName("bvn")]
    public string? Bvn { get; set; }

    /// <summary>Customer National Identification Number (NIN) if available.</summary>
    [JsonPropertyName("nin")]
    public string? Nin { get; set; }

    /// <summary>Flag indicating whether to provision accounts across all partner banks.</summary>
    [JsonPropertyName("getAllAvailableBanks")]
    public bool GetAllAvailableBanks { get; set; } = true;
}

/// <summary>
/// Monnify reserved account creation response payload.
/// </summary>
public sealed class MonnifyCreateReservedAccountResponseBody
{
    /// <summary>Contract code.</summary>
    [JsonPropertyName("contractCode")]
    public string? ContractCode { get; set; }

    /// <summary>Account reference.</summary>
    [JsonPropertyName("accountReference")]
    public string? AccountReference { get; set; }

    /// <summary>Account name.</summary>
    [JsonPropertyName("accountName")]
    public string? AccountName { get; set; }

    /// <summary>Currency code.</summary>
    [JsonPropertyName("currencyCode")]
    public string? CurrencyCode { get; set; }

    /// <summary>Customer email.</summary>
    [JsonPropertyName("customerEmail")]
    public string? CustomerEmail { get; set; }

    /// <summary>Customer name.</summary>
    [JsonPropertyName("customerName")]
    public string? CustomerName { get; set; }

    /// <summary>Allocated bank accounts list.</summary>
    [JsonPropertyName("accounts")]
    public List<MonnifyAccountDetails>? Accounts { get; set; }

    /// <summary>Collection channel.</summary>
    [JsonPropertyName("collectionChannel")]
    public string? CollectionChannel { get; set; }

    /// <summary>Reservation status.</summary>
    [JsonPropertyName("reservationStatus")]
    public string? ReservationStatus { get; set; }
}

/// <summary>
/// Account details allocated to a reserved virtual account.
/// </summary>
public sealed class MonnifyAccountDetails
{
    /// <summary>Assigned bank code (e.g. 035 for Wema Bank).</summary>
    [JsonPropertyName("bankCode")]
    public string? BankCode { get; set; }

    /// <summary>Assigned bank institution name.</summary>
    [JsonPropertyName("bankName")]
    public string? BankName { get; set; }

    /// <summary>Assigned 10-digit NUBAN virtual account number.</summary>
    [JsonPropertyName("accountNumber")]
    public string? AccountNumber { get; set; }

    /// <summary>Account beneficiary name.</summary>
    [JsonPropertyName("accountName")]
    public string? AccountName { get; set; }
}

/// <summary>
/// Monnify transaction query response payload.
/// </summary>
public sealed class MonnifyTransactionResponseBody
{
    /// <summary>Monnify transaction reference.</summary>
    [JsonPropertyName("transactionReference")]
    public string? TransactionReference { get; set; }

    /// <summary>Merchant payment reference.</summary>
    [JsonPropertyName("paymentReference")]
    public string? PaymentReference { get; set; }

    /// <summary>Gross amount paid by customer.</summary>
    [JsonPropertyName("amountPaid")]
    public decimal? AmountPaid { get; set; }

    /// <summary>Total payable amount.</summary>
    [JsonPropertyName("totalPayable")]
    public decimal? TotalPayable { get; set; }

    /// <summary>Net settlement amount credited after provider fees.</summary>
    [JsonPropertyName("settlementAmount")]
    public decimal? SettlementAmount { get; set; }

    /// <summary>Timestamp string when payment was received.</summary>
    [JsonPropertyName("paidOn")]
    public string? PaidOn { get; set; }

    /// <summary>Payment lifecycle status (e.g. PAID, FAILED, PENDING).</summary>
    [JsonPropertyName("paymentStatus")]
    public string? PaymentStatus { get; set; }

    /// <summary>Payment description.</summary>
    [JsonPropertyName("paymentDescription")]
    public string? PaymentDescription { get; set; }

    /// <summary>Currency code.</summary>
    [JsonPropertyName("currencyCode")]
    public string? CurrencyCode { get; set; }

    /// <summary>Payment method used (e.g. ACCOUNT_TRANSFER, CARD).</summary>
    [JsonPropertyName("paymentMethod")]
    public string? PaymentMethod { get; set; }

    /// <summary>Destination account information.</summary>
    [JsonPropertyName("destinationAccountInformation")]
    public MonnifyDestinationAccountInformation? DestinationAccountInformation { get; set; }
}

/// <summary>
/// Destination account information on transaction query.
/// </summary>
public sealed class MonnifyDestinationAccountInformation
{
    /// <summary>Destination bank code.</summary>
    [JsonPropertyName("bankCode")]
    public string? BankCode { get; set; }

    /// <summary>Destination bank name.</summary>
    [JsonPropertyName("bankName")]
    public string? BankName { get; set; }

    /// <summary>Destination account number.</summary>
    [JsonPropertyName("accountNumber")]
    public string? AccountNumber { get; set; }
}

/// <summary>
/// Monnify single transfer / disbursement request payload.
/// </summary>
public sealed class MonnifySingleTransferRequest
{
    /// <summary>Amount to transfer.</summary>
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    /// <summary>Unique transfer reference.</summary>
    [JsonPropertyName("reference")]
    public string Reference { get; set; } = string.Empty;

    /// <summary>Transfer narration.</summary>
    [JsonPropertyName("narration")]
    public string Narration { get; set; } = string.Empty;

    /// <summary>Destination bank institution code (e.g. 058, 044, 011).</summary>
    [JsonPropertyName("destinationBankCode")]
    public string DestinationBankCode { get; set; } = string.Empty;

    /// <summary>Destination 10-digit NUBAN account number.</summary>
    [JsonPropertyName("destinationAccountNumber")]
    public string DestinationAccountNumber { get; set; } = string.Empty;

    /// <summary>Destination account holder name.</summary>
    [JsonPropertyName("destinationAccountName")]
    public string? DestinationAccountName { get; set; }

    /// <summary>Currency code (e.g. NGN).</summary>
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "NGN";

    /// <summary>Source account/wallet number from which transfer is debited.</summary>
    [JsonPropertyName("sourceAccountNumber")]
    public string SourceAccountNumber { get; set; } = string.Empty;

    /// <summary>Whether transfer execution should be asynchronous.</summary>
    [JsonPropertyName("async")]
    public bool Async { get; set; }
}

/// <summary>
/// Monnify single transfer / disbursement response payload.
/// </summary>
public sealed class MonnifySingleTransferResponseBody
{
    /// <summary>Transaction reference.</summary>
    [JsonPropertyName("reference")]
    public string? Reference { get; set; }

    /// <summary>Transfer amount.</summary>
    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }

    /// <summary>Narration.</summary>
    [JsonPropertyName("narration")]
    public string? Narration { get; set; }

    /// <summary>Currency code.</summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    /// <summary>Provider transaction fee if charged.</summary>
    [JsonPropertyName("fee")]
    public decimal? Fee { get; set; }

    /// <summary>Transfer status (SUCCESS, PENDING, FAILED, IN_PROGRESS).</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>Monnify internal transaction reference.</summary>
    [JsonPropertyName("transactionReference")]
    public string? TransactionReference { get; set; }

    /// <summary>Transaction description or error message.</summary>
    [JsonPropertyName("transactionDescription")]
    public string? TransactionDescription { get; set; }

    /// <summary>Resolved destination account name.</summary>
    [JsonPropertyName("destinationAccountName")]
    public string? DestinationAccountName { get; set; }

    /// <summary>Destination bank code.</summary>
    [JsonPropertyName("destinationBankCode")]
    public string? DestinationBankCode { get; set; }

    /// <summary>Destination account number.</summary>
    [JsonPropertyName("destinationAccountNumber")]
    public string? DestinationAccountNumber { get; set; }
}

/// <summary>
/// Monnify account validation response payload.
/// </summary>
public sealed class MonnifyAccountValidationResponseBody
{
    /// <summary>Validated account number.</summary>
    [JsonPropertyName("accountNumber")]
    public string? AccountNumber { get; set; }

    /// <summary>Resolved account beneficiary name.</summary>
    [JsonPropertyName("accountName")]
    public string? AccountName { get; set; }

    /// <summary>Validated bank code.</summary>
    [JsonPropertyName("bankCode")]
    public string? BankCode { get; set; }
}

/// <summary>
/// Monnify disbursement search / summary response payload.
/// </summary>
public sealed class MonnifyDisbursementSummaryResponseBody
{
    /// <summary>Merchant transfer reference.</summary>
    [JsonPropertyName("reference")]
    public string? Reference { get; set; }

    /// <summary>Monnify transaction reference.</summary>
    [JsonPropertyName("transactionReference")]
    public string? TransactionReference { get; set; }

    /// <summary>Transfer amount.</summary>
    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }

    /// <summary>Provider fee.</summary>
    [JsonPropertyName("fee")]
    public decimal? Fee { get; set; }

    /// <summary>Currency code.</summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    /// <summary>Disbursement status (SUCCESS, FAILED, PENDING, IN_PROGRESS, REVERSED).</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>Destination account name.</summary>
    [JsonPropertyName("destinationAccountName")]
    public string? DestinationAccountName { get; set; }

    /// <summary>Destination bank code.</summary>
    [JsonPropertyName("destinationBankCode")]
    public string? DestinationBankCode { get; set; }

    /// <summary>Destination account number.</summary>
    [JsonPropertyName("destinationAccountNumber")]
    public string? DestinationAccountNumber { get; set; }

    /// <summary>Status message or reason.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
