using System.Security.Cryptography;
using CebizPay.Application.Common.Interfaces.Referrals;

namespace CebizPay.Application.Common.Referrals;

/// <summary>
/// Generates cryptographically random, collision-resistant referral codes.
/// Format: "CBZ" prefix + 6 characters from an unambiguous alphanumeric alphabet.
/// Safe for public display and sharing; contains no sensitive IDs or user data.
/// </summary>
public sealed class ReferralCodeGenerator : IReferralCodeGenerator
{
    private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"; // 32 chars without 0, O, 1, I
    private const int CodeLength = 6;

    /// <inheritdoc/>
    public string GenerateCode()
    {
        var chars = new char[CodeLength];
        var bytes = RandomNumberGenerator.GetBytes(CodeLength);

        for (int i = 0; i < CodeLength; i++)
        {
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        }

        return $"CBZ{new string(chars)}";
    }
}
