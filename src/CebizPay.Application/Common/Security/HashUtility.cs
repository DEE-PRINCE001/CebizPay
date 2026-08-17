using System.Security.Cryptography;
using System.Text;

namespace CebizPay.Application.Common.Security;

/// <summary>
/// Cryptographic hashing utility methods.
/// </summary>
public static class HashUtility
{
    /// <summary>
    /// Computes SHA256 hex string hash for a given string payload.
    /// </summary>
    /// <param name="payload">Input string payload.</param>
    /// <returns>Upper-case hex-encoded SHA256 hash string.</returns>
    public static string ComputeSha256(string? payload)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload ?? string.Empty));
        return Convert.ToHexString(bytes);
    }
}
