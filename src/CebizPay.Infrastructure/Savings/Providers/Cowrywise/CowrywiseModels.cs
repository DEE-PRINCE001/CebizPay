#pragma warning disable CA1848, CS1591
using System.Text.Json.Serialization;

namespace CebizPay.Infrastructure.Savings.Providers.Cowrywise;

/// <summary>
/// Generic Cowrywise API response wrapper.
/// </summary>
public sealed class CowrywiseApiResponse<T>
{
    [JsonPropertyName("status")]
    public bool Status { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }
}

/// <summary>
/// OAuth2 token response returned from Cowrywise auth endpoint.
/// </summary>
public sealed class CowrywiseAuthTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; } = 3600;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";
}

/// <summary>
/// Payload to create an account at Cowrywise.
/// </summary>
public sealed class CowrywiseCreateAccountRequest
{
    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("last_name")]
    public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("phone_number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [JsonPropertyName("identity")]
    public CowrywiseIdentity? Identity { get; set; }
}

/// <summary>
/// Identity verification details for Cowrywise account creation.
/// </summary>
public sealed class CowrywiseIdentity
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "bvn";

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Customer account details returned from Cowrywise.
/// </summary>
public sealed class CowrywiseAccountData
{
    [JsonPropertyName("account_id")]
    public string AccountId { get; set; } = string.Empty;

    [JsonPropertyName("wallet_id")]
    public string? WalletId { get; set; }

    [JsonPropertyName("is_verified")]
    public bool IsVerified { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }
}

/// <summary>
/// Product rate quote data returned from Cowrywise.
/// </summary>
public sealed class CowrywiseRateData
{
    [JsonPropertyName("product_code")]
    public string ProductCode { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("rate")]
    public decimal Rate { get; set; }

    [JsonPropertyName("min_days")]
    public int MinDays { get; set; } = 30;

    [JsonPropertyName("max_days")]
    public int MaxDays { get; set; } = 730;

    [JsonPropertyName("penalty_rate")]
    public decimal PenaltyRate { get; set; }
}

/// <summary>
/// Payload to create a savings plan at Cowrywise.
/// </summary>
public sealed class CowrywiseCreateSavingsRequest
{
    [JsonPropertyName("account_id")]
    public string AccountId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "NGN";

    [JsonPropertyName("deposit_type")]
    public string DepositType { get; set; } = "fixed";

    [JsonPropertyName("duration")]
    public int Duration { get; set; }

    [JsonPropertyName("interest_enabled")]
    public bool InterestEnabled { get; set; } = true;

    [JsonPropertyName("target_amount")]
    public decimal? TargetAmount { get; set; }

    [JsonPropertyName("idempotency_key")]
    public string IdempotencyKey { get; set; } = string.Empty;
}

/// <summary>
/// Response data from Cowrywise plan creation.
/// </summary>
public sealed class CowrywiseSavingsData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "active";

    [JsonPropertyName("rate")]
    public decimal Rate { get; set; }

    [JsonPropertyName("maturity_date")]
    public DateTime MaturityDate { get; set; }
}

/// <summary>
/// Payload to fund a savings plan.
/// </summary>
public sealed class CowrywiseFundSavingsRequest
{
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "NGN";

    [JsonPropertyName("transaction_reference")]
    public string TransactionReference { get; set; } = string.Empty;
}

/// <summary>
/// Response data from funding a savings plan.
/// </summary>
public sealed class CowrywiseFundingData
{
    [JsonPropertyName("transaction_id")]
    public string TransactionId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "completed";

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }
}

/// <summary>
/// Savings position valuation data from Cowrywise.
/// </summary>
public sealed class CowrywisePositionData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("principal_balance")]
    public decimal PrincipalBalance { get; set; }

    [JsonPropertyName("accrued_interest")]
    public decimal AccruedInterest { get; set; }

    [JsonPropertyName("total_yield")]
    public decimal TotalYield { get; set; }

    [JsonPropertyName("is_matured")]
    public bool IsMatured { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "active";
}

/// <summary>
/// Payload to liquidate a savings plan.
/// </summary>
public sealed class CowrywiseLiquidationRequest
{
    [JsonPropertyName("account_id")]
    public string AccountId { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("is_early")]
    public bool IsEarly { get; set; }

    [JsonPropertyName("transaction_reference")]
    public string TransactionReference { get; set; } = string.Empty;
}

/// <summary>
/// Response data from Cowrywise plan liquidation.
/// </summary>
public sealed class CowrywiseLiquidationData
{
    [JsonPropertyName("transaction_id")]
    public string TransactionId { get; set; } = string.Empty;

    [JsonPropertyName("gross_amount")]
    public decimal GrossAmount { get; set; }

    [JsonPropertyName("penalty_amount")]
    public decimal PenaltyAmount { get; set; }

    [JsonPropertyName("forfeited_interest")]
    public decimal ForfeitedInterest { get; set; }

    [JsonPropertyName("net_amount")]
    public decimal NetAmount { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "completed";
}
