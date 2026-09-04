using System.Text.RegularExpressions;
using CebizPay.Domain.Vas.Enums;

namespace CebizPay.Application.Common.Utils;

/// <summary>
/// Utility for standardizing, cleaning, and validating Nigerian mobile telephone numbers
/// and detecting network operators based on national dialing plan prefixes.
/// </summary>
public static partial class PhoneNormalizer
{
    private static readonly Regex CleanRegex = new(@"[^\d]", RegexOptions.Compiled);

    /// <summary>
    /// Normalizes a phone number to international 13-digit format (e.g. "2348031234567").
    /// </summary>
    public static string NormalizeInternational(string rawPhoneNumber)
    {
        if (string.IsNullOrWhiteSpace(rawPhoneNumber))
            return string.Empty;

        var digits = CleanRegex.Replace(rawPhoneNumber, string.Empty);

        if (digits.StartsWith("234", StringComparison.Ordinal) && digits.Length == 13)
            return digits;

        if (digits.StartsWith('0') && digits.Length == 11)
            return $"234{digits[1..]}";

        if (digits.Length == 10)
            return $"234{digits}";

        return digits;
    }

    /// <summary>
    /// Normalizes a phone number to standard national 11-digit format (e.g. "08031234567").
    /// </summary>
    public static string NormalizeNational(string rawPhoneNumber)
    {
        if (string.IsNullOrWhiteSpace(rawPhoneNumber))
            return string.Empty;

        var digits = CleanRegex.Replace(rawPhoneNumber, string.Empty);

        if (digits.StartsWith("234", StringComparison.Ordinal) && digits.Length == 13)
            return $"0{digits[3..]}";

        if (digits.StartsWith('0') && digits.Length == 11)
            return digits;

        if (digits.Length == 10)
            return $"0{digits}";

        return digits;
    }

    /// <summary>
    /// Normalizes a phone number to canonical E.164 format (e.g. "+2348031234567").
    /// Handles standard Nigerian mobile numbers across all supported input styles
    /// (local 11-digit leading 0, 10-digit without leading 0, 13-digit with 234, 14-digit with 2340,
    /// formatted with spaces/hyphens/parentheses), as well as general international E.164 numbers.
    /// </summary>
    public static string NormalizeE164(string? rawPhoneNumber)
    {
        if (string.IsNullOrWhiteSpace(rawPhoneNumber))
            return string.Empty;

        var clean = rawPhoneNumber.Trim();
        var digits = CleanRegex.Replace(clean, string.Empty);
        if (string.IsNullOrEmpty(digits))
            return string.Empty;

        // Nigerian 14 digits with 2340 prefix: +234 (0) 803 123 4567 -> +2348031234567
        if (digits.StartsWith("2340", StringComparison.Ordinal) && digits.Length == 14)
            return $"+234{digits[4..]}";

        // Nigerian local 11 digits: 08031234567 -> +2348031234567
        if (digits.StartsWith('0') && digits.Length == 11)
            return $"+234{digits[1..]}";

        // Nigerian 10 digits without leading 0: 8031234567 -> +2348031234567
        if (digits.Length == 10 && (digits.StartsWith('7') || digits.StartsWith('8') || digits.StartsWith('9')))
            return $"+234{digits}";

        // Nigerian 13 digits: 2348031234567 -> +2348031234567
        if (digits.StartsWith("234", StringComparison.Ordinal) && digits.Length == 13)
            return $"+{digits}";

        // General international: ensure leading '+'
        return clean.StartsWith('+') ? $"+{digits}" : $"+{digits}";
    }

    /// <summary>
    /// Validates whether the given string represents an acceptable mobile phone number
    /// (valid Nigerian mobile or standard international E.164 number between 8 and 15 digits).
    /// </summary>
    public static bool IsValidPhoneNumber(string? rawPhoneNumber)
    {
        if (string.IsNullOrWhiteSpace(rawPhoneNumber))
            return false;

        var e164 = NormalizeE164(rawPhoneNumber);
        if (string.IsNullOrEmpty(e164) || !e164.StartsWith('+'))
            return false;

        var digits = e164[1..];
        if (digits.Length < 8 || digits.Length > 15)
            return false;

        return true;
    }

    /// <summary>
    /// Validates whether the given string represents a valid Nigerian mobile phone number.
    /// </summary>
    public static bool IsValidNigerianPhoneNumber(string? rawPhoneNumber)
    {
        if (string.IsNullOrWhiteSpace(rawPhoneNumber))
            return false;

        var national = NormalizeNational(rawPhoneNumber);
        if (national.Length != 11 || !national.StartsWith('0'))
            return false;

        // Valid prefix check (070, 080, 081, 090, 091)
        var prefix = national[..3];
        return prefix is "070" or "080" or "081" or "090" or "091";
    }

    /// <summary>
    /// Resolves the telecommunications operator from a mobile phone number prefix.
    /// </summary>
    public static VasNetwork? DetectNetworkFromPrefix(string rawPhoneNumber)
    {
        var national = NormalizeNational(rawPhoneNumber);
        if (national.Length < 4)
            return null;

        var prefix4 = national[..4];

        // MTN
        if (prefix4 is "0803" or "0806" or "0703" or "0706" or "0813" or "0816" or "0810" or "0814" or "0903" or "0906" or "0913" or "0916")
            return VasNetwork.Mtn;

        // Airtel
        if (prefix4 is "0802" or "0808" or "0708" or "0812" or "0701" or "0902" or "0901" or "0907" or "0912")
            return VasNetwork.Airtel;

        // Glo
        if (prefix4 is "0805" or "0807" or "0705" or "0815" or "0811" or "0905" or "0915")
            return VasNetwork.Glo;

        // 9mobile
        if (prefix4 is "0809" or "0817" or "0818" or "0909" or "0908")
            return VasNetwork.NineMobile;

        return null;
    }
}
