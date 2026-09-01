#pragma warning disable CS1591
using System.Text.Json.Serialization;

namespace CebizPay.Infrastructure.Compliance.SmileId.Models;

public sealed class SmileIdPartnerParams
{
    [JsonPropertyName("job_id")]
    public string JobId { get; set; } = string.Empty;

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("job_type")]
    public int JobType { get; set; }
}

public sealed class SmileIdIdInfo
{
    [JsonPropertyName("country")]
    public string Country { get; set; } = "NG";

    [JsonPropertyName("id_type")]
    public string IdType { get; set; } = string.Empty;

    [JsonPropertyName("id_number")]
    public string IdNumber { get; set; } = string.Empty;

    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }

    [JsonPropertyName("dob")]
    public string? Dob { get; set; }

    [JsonPropertyName("entered")]
    public string Entered { get; set; } = "true";
}

public sealed class SmileIdImageInfo
{
    [JsonPropertyName("image_type_id")]
    public int ImageTypeId { get; set; }

    [JsonPropertyName("image")]
    public string Image { get; set; } = string.Empty;
}

public sealed class SmileIdIdVerificationRequest
{
    [JsonPropertyName("partner_id")]
    public string PartnerId { get; set; } = string.Empty;

    [JsonPropertyName("partner_params")]
    public SmileIdPartnerParams PartnerParams { get; set; } = new();

    [JsonPropertyName("id_info")]
    public SmileIdIdInfo IdInfo { get; set; } = new();

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;
}

public sealed class SmileIdBiometricKycRequest
{
    [JsonPropertyName("partner_id")]
    public string PartnerId { get; set; } = string.Empty;

    [JsonPropertyName("partner_params")]
    public SmileIdPartnerParams PartnerParams { get; set; } = new();

    [JsonPropertyName("id_info")]
    public SmileIdIdInfo? IdInfo { get; set; }

    [JsonPropertyName("images")]
    public List<SmileIdImageInfo> Images { get; set; } = new();

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;
}

public sealed class SmileIdDocumentVerificationRequest
{
    [JsonPropertyName("partner_id")]
    public string PartnerId { get; set; } = string.Empty;

    [JsonPropertyName("partner_params")]
    public SmileIdPartnerParams PartnerParams { get; set; } = new();

    [JsonPropertyName("id_info")]
    public SmileIdIdInfo? IdInfo { get; set; }

    [JsonPropertyName("images")]
    public List<SmileIdImageInfo> Images { get; set; } = new();

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;
}

public sealed class SmileIdAmlRequest
{
    [JsonPropertyName("partner_id")]
    public string PartnerId { get; set; } = string.Empty;

    [JsonPropertyName("partner_params")]
    public SmileIdPartnerParams PartnerParams { get; set; } = new();

    [JsonPropertyName("search_type")]
    public string SearchType { get; set; } = "individual";

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;
}

public sealed class SmileIdBusinessVerificationRequest
{
    [JsonPropertyName("partner_id")]
    public string PartnerId { get; set; } = string.Empty;

    [JsonPropertyName("partner_params")]
    public SmileIdPartnerParams PartnerParams { get; set; } = new();

    [JsonPropertyName("country")]
    public string Country { get; set; } = "NG";

    [JsonPropertyName("registration_number")]
    public string RegistrationNumber { get; set; } = string.Empty;

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;
}

public sealed class SmileIdJobResponse
{
    [JsonPropertyName("job_id")]
    public string? JobId { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("result_code")]
    public string? ResultCode { get; set; }

    [JsonPropertyName("result_text")]
    public string? ResultText { get; set; }

    [JsonPropertyName("confidence_value")]
    public decimal? ConfidenceValue { get; set; }

    [JsonPropertyName("Actions")]
    public SmileIdActions? Actions { get; set; }

    [JsonPropertyName("FullData")]
    public Dictionary<string, object>? FullData { get; set; }
}

public sealed class SmileIdActions
{
    [JsonPropertyName("Verify_ID_Number")]
    public string? VerifyIdNumber { get; set; }

    [JsonPropertyName("Liveness_Check")]
    public string? LivenessCheck { get; set; }

    [JsonPropertyName("Register_Selfie")]
    public string? RegisterSelfie { get; set; }

    [JsonPropertyName("Human_Review_Compare")]
    public string? HumanReviewCompare { get; set; }
}
