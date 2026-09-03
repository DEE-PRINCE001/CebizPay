using System.Security.Cryptography;
using CebizPay.Application.Common.Interfaces.Support;

namespace CebizPay.Application.Common.Support;

/// <summary>
/// Generates customer-facing ticket numbers in format "CBZ-SUP-yyyy-XXXXXX".
/// Uses unambiguous alphanumeric characters and cryptographic randomness.
/// </summary>
public sealed class SupportTicketNumberGenerator : ISupportTicketNumberGenerator
{
    private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"; // 32 unambiguous chars
    private const int CodeLength = 6;

    /// <inheritdoc/>
    public string GenerateTicketNumber()
    {
        var chars = new char[CodeLength];
        var bytes = RandomNumberGenerator.GetBytes(CodeLength);

        for (int i = 0; i < CodeLength; i++)
        {
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        }

        var year = DateTime.UtcNow.Year;
        return $"CBZ-SUP-{year}-{new string(chars)}";
    }
}
