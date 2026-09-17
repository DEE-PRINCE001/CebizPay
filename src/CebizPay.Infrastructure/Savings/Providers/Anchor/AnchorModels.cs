#pragma warning disable CA1848, CS1591
using System.Text.Json.Serialization;

namespace CebizPay.Infrastructure.Savings.Providers.Anchor;

/// <summary>
/// Root container for JSON:API style Anchor requests and responses.
/// </summary>
public sealed class AnchorResourceEnvelope<T>
{
    [JsonPropertyName("data")]
    public T? Data { get; set; }
}

/// <summary>
/// Anchor resource item with type, id, attributes, and relationships.
/// </summary>
public sealed class AnchorResource<TAttributes>
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("attributes")]
    public TAttributes? Attributes { get; set; }

    [JsonPropertyName("relationships")]
    public Dictionary<string, AnchorRelationship>? Relationships { get; set; }
}

/// <summary>
/// Anchor JSON:API relationship structure.
/// </summary>
public sealed class AnchorRelationship
{
    [JsonPropertyName("data")]
    public AnchorResourceRef? Data { get; set; }
}

/// <summary>
/// Reference pointer for related Anchor resources.
/// </summary>
public sealed class AnchorResourceRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Customer attributes payload for creating an individual customer at Anchor.
/// </summary>
public sealed class AnchorIndividualCustomerAttributes
{
    [JsonPropertyName("fullName")]
    public AnchorFullName FullName { get; set; } = new();

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("phoneNumber")]
    public string PhoneNumber { get; set; } = string.Empty;

    [JsonPropertyName("bvn")]
    public string? Bvn { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }
}

/// <summary>
/// Full name structure used by Anchor.
/// </summary>
public sealed class AnchorFullName
{
    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("lastName")]
    public string LastName { get; set; } = string.Empty;
}

/// <summary>
/// Attributes for creating and querying an Anchor subledger account.
/// </summary>
public sealed class AnchorSubAccountAttributes
{
    [JsonPropertyName("createVirtualNuban")]
    public bool CreateVirtualNuban { get; set; }

    [JsonPropertyName("accountType")]
    public string? AccountType { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("balance")]
    public AnchorBalance? Balance { get; set; }
}

/// <summary>
/// Balance representation on Anchor accounts.
/// </summary>
public sealed class AnchorBalance
{
    [JsonPropertyName("availableBalance")]
    public decimal AvailableBalance { get; set; }

    [JsonPropertyName("ledgerBalance")]
    public decimal LedgerBalance { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "NGN";
}

/// <summary>
/// Attributes for creating Anchor transfers (e.g. BookTransfer).
/// </summary>
public sealed class AnchorTransferAttributes
{
    [JsonPropertyName("transferType")]
    public string TransferType { get; set; } = "BookTransfer";

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "NGN";

    [JsonPropertyName("reference")]
    public string Reference { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string? Status { get; set; }
}
