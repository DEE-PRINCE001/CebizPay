#pragma warning disable CS1591
using System.Text.Json.Serialization;

namespace CebizPay.Infrastructure.Compliance.Ninja.Models;

public sealed class NinjaApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("reference")]
    public string? Reference { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed class NinjaIdentityRequest
{
    [JsonPropertyName("identifier")]
    public string Identifier { get; set; } = string.Empty;

    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("last_name")]
    public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("dob")]
    public string? Dob { get; set; }
}

public sealed class NinjaIdentityData
{
    [JsonPropertyName("match")]
    public bool Match { get; set; }

    [JsonPropertyName("confidence_score")]
    public decimal? ConfidenceScore { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }
}

public sealed class NinjaCacRequest
{
    [JsonPropertyName("rc_number")]
    public string RcNumber { get; set; } = string.Empty;

    [JsonPropertyName("company_name")]
    public string CompanyName { get; set; } = string.Empty;
}

public sealed class NinjaCacData
{
    [JsonPropertyName("rc_number")]
    public string? RcNumber { get; set; }

    [JsonPropertyName("company_name")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("company_type")]
    public string? CompanyType { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("registration_date")]
    public string? RegistrationDate { get; set; }
}

public sealed class NinjaAmlRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "individual";

    [JsonPropertyName("country")]
    public string Country { get; set; } = "NG";
}

public sealed class NinjaAmlData
{
    [JsonPropertyName("matches_count")]
    public int MatchesCount { get; set; }

    [JsonPropertyName("risk_level")]
    public string? RiskLevel { get; set; }

    [JsonPropertyName("pep_match")]
    public bool PepMatch { get; set; }

    [JsonPropertyName("sanction_match")]
    public bool SanctionMatch { get; set; }
}
