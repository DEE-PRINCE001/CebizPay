#pragma warning disable CS1591
using System.Text.Json.Serialization;

namespace CebizPay.Infrastructure.Compliance.Dojah.Models;

/// <summary>
/// Root wrapper for Dojah API responses.
/// </summary>
public sealed class DojahApiResponse<T>
{
    [JsonPropertyName("entity")]
    public T? Entity { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public sealed class DojahBvnVerifyResponseBody
{
    [JsonPropertyName("bvn")]
    public string? Bvn { get; set; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }

    [JsonPropertyName("middle_name")]
    public string? MiddleName { get; set; }

    [JsonPropertyName("date_of_birth")]
    public string? DateOfBirth { get; set; }

    [JsonPropertyName("phone_number1")]
    public string? PhoneNumber1 { get; set; }

    [JsonPropertyName("status")]
    public bool Status { get; set; }
}

public sealed class DojahNinVerifyResponseBody
{
    [JsonPropertyName("nin")]
    public string? Nin { get; set; }

    [JsonPropertyName("firstname")]
    public string? FirstName { get; set; }

    [JsonPropertyName("surname")]
    public string? Surname { get; set; }

    [JsonPropertyName("middlename")]
    public string? MiddleName { get; set; }

    [JsonPropertyName("birthdate")]
    public string? BirthDate { get; set; }

    [JsonPropertyName("telephoneno")]
    public string? TelephoneNo { get; set; }

    [JsonPropertyName("status")]
    public bool Status { get; set; }
}

public sealed class DojahPhotoIdVerifyRequest
{
    [JsonPropertyName("photoid_image")]
    public string PhotoIdImage { get; set; } = string.Empty;

    [JsonPropertyName("selfie_image")]
    public string SelfieImage { get; set; } = string.Empty;
}

public sealed class DojahPhotoIdVerifyResponseBody
{
    [JsonPropertyName("confidence_value")]
    public decimal? ConfidenceValue { get; set; }

    [JsonPropertyName("match")]
    public bool Match { get; set; }

    [JsonPropertyName("selfie_verification")]
    public bool SelfieVerification { get; set; }
}

public sealed class DojahDocumentAnalysisRequest
{
    [JsonPropertyName("image")]
    public string Image { get; set; } = string.Empty;

    [JsonPropertyName("doc_type")]
    public string? DocType { get; set; }
}

public sealed class DojahDocumentAnalysisResponseBody
{
    [JsonPropertyName("document_type")]
    public string? DocumentType { get; set; }

    [JsonPropertyName("document_number")]
    public string? DocumentNumber { get; set; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("expiry_date")]
    public string? ExpiryDate { get; set; }
}

public sealed class DojahAmlScreeningRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("date_of_birth")]
    public string? DateOfBirth { get; set; }
}

public sealed class DojahAmlScreeningResponseBody
{
    [JsonPropertyName("match_status")]
    public string? MatchStatus { get; set; }

    [JsonPropertyName("number_of_matches")]
    public int NumberOfMatches { get; set; }

    [JsonPropertyName("pep")]
    public bool Pep { get; set; }

    [JsonPropertyName("sanction")]
    public bool Sanction { get; set; }
}

public sealed class DojahCacResponseBody
{
    [JsonPropertyName("rc_number")]
    public string? RcNumber { get; set; }

    [JsonPropertyName("company_name")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("company_type")]
    public string? CompanyType { get; set; }

    [JsonPropertyName("registration_date")]
    public string? RegistrationDate { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("directors")]
    public List<DojahDirector>? Directors { get; set; }
}

public sealed class DojahDirector
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("designation")]
    public string? Designation { get; set; }
}
