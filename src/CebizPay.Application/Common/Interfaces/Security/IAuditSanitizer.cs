namespace CebizPay.Application.Common.Interfaces.Security;

/// <summary>
/// Service responsible for sanitizing and redacting sensitive data (passwords, PINs, tokens, keys, PAN, CVV, secrets)
/// before persisting or serializing into audit log records.
/// </summary>
public interface IAuditSanitizer
{
    /// <summary>
    /// Serializes an object to a sanitized JSON string with all sensitive fields redacted.
    /// </summary>
    string? Sanitize(object? payload);

    /// <summary>
    /// Sanitizes an existing JSON string or plain text, redacting all sensitive key-value pairs.
    /// </summary>
    string? SanitizeJsonString(string? json);
}
