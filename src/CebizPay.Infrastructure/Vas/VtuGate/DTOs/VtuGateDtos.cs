using System.Text.Json;
using System.Text.Json.Serialization;

namespace CebizPay.Infrastructure.Vas.VtuGate.DTOs;

/// <summary>Request payload for VTUGATE airtime purchase.</summary>
public sealed record VtuGateAirtimeRequest(
    [property: JsonPropertyName("request_id")] string RequestId,
    [property: JsonPropertyName("phone")] string Phone,
    [property: JsonPropertyName("network")] string Network,
    [property: JsonPropertyName("amount")] decimal Amount);

/// <summary>Request payload for VTUGATE mobile data purchase.</summary>
public sealed record VtuGateDataRequest(
    [property: JsonPropertyName("request_id")] string RequestId,
    [property: JsonPropertyName("phone")] string Phone,
    [property: JsonPropertyName("network")] string Network,
    [property: JsonPropertyName("plan_id")] string PlanId,
    [property: JsonPropertyName("amount")] decimal Amount);

/// <summary>Standard JSON response envelope returned by VTUGATE.</summary>
public sealed record VtuGateResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("reference")] string? Reference,
    [property: JsonPropertyName("transaction_id")] string? TransactionId,
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("data")] JsonElement? Data);

/// <summary>Catalog item representation for data bundle plans from VTUGATE.</summary>
public sealed record VtuGateBundleItem(
    [property: JsonPropertyName("plan_id")] string PlanId,
    [property: JsonPropertyName("network")] string Network,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("volume")] string? Volume,
    [property: JsonPropertyName("validity")] string? Validity,
    [property: JsonPropertyName("amount")] decimal Amount);
