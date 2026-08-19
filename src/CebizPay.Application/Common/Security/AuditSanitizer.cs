using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CebizPay.Application.Common.Interfaces.Security;

namespace CebizPay.Application.Common.Security;

/// <summary>
/// Implements sensitive data redaction for audit trails.
/// Ensures passwords, PINs, tokens, keys, card details, and credentials are never persisted in audit logs.
/// </summary>
public sealed class AuditSanitizer : IAuditSanitizer
{
    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly HashSet<string> ExactSensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "passwordhash", "currentpassword", "newpassword", "confirmpassword", "oldpassword",
        "pin", "transactionpin", "pinhash", "currentpin", "newpin", "oldpin",
        "otp", "otpcode", "verificationcode", "smscode", "code",
        "mfa", "mfacode", "mfasecret", "authenticatorsecret", "totpsecret", "secret", "secretkey",
        "jwt", "token", "accesstoken", "refreshtoken", "bearertoken", "idtoken",
        "apikey", "apisecret", "privatekey", "clientsecret",
        "pan", "cardnumber", "cvv", "cvc", "securitycode", "cardcvv", "cardpan",
        "connectionstring", "dbpassword", "credential", "credentials"
    };

    private static readonly string[] SensitiveSubstrings =
    [
        "password", "pin", "pinhash", "secret", "token", "apikey", "cvv", "cvc", "pan", "otp"
    ];

    /// <inheritdoc/>
    public string? Sanitize(object? payload)
    {
        if (payload is null)
            return null;

        if (payload is string str)
            return SanitizeJsonString(str);

        try
        {
            var jsonNode = JsonSerializer.SerializeToNode(payload, DefaultJsonOptions);
            if (jsonNode is null)
                return null;

            RedactNode(jsonNode);
            return jsonNode.ToJsonString(DefaultJsonOptions);
        }
        catch
        {
            // If complex object fails serialization, return a safe fallback representation
            return "{\"sanitized\":true}";
        }
    }

    /// <inheritdoc/>
    public string? SanitizeJsonString(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return json;

        try
        {
            var jsonNode = JsonNode.Parse(json);
            if (jsonNode is not null)
            {
                RedactNode(jsonNode);
                return jsonNode.ToJsonString(DefaultJsonOptions);
            }
        }
        catch (JsonException)
        {
            // If string is not valid JSON, use regex redaction for common key:value sensitive patterns
            return SanitizePlainText(json);
        }

        return json;
    }

    private static void RedactNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var properties = obj.ToList();
            foreach (var property in properties)
            {
                if (IsSensitiveKey(property.Key))
                {
                    obj[property.Key] = "[REDACTED]";
                }
                else if (property.Value is not null)
                {
                    RedactNode(property.Value);
                }
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item is not null)
                {
                    RedactNode(item);
                }
            }
        }
    }

    private static bool IsSensitiveKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        var normalizedKey = key.Replace("-", string.Empty)
                               .Replace("_", string.Empty)
                               .ToLowerInvariant();

        if (ExactSensitiveKeys.Contains(normalizedKey))
            return true;

        foreach (var sub in SensitiveSubstrings)
        {
            if (normalizedKey.Contains(sub, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string SanitizePlainText(string text)
    {
        // Redact common key=value or key:value sensitive patterns in plain text
        var pattern = @"(?i)(password|pin|token|secret|apikey|cvv|pan)\s*[:=]\s*[""']?([^,\s""'}]+)[""']?";
        return Regex.Replace(text, pattern, "$1: [REDACTED]");
    }
}
