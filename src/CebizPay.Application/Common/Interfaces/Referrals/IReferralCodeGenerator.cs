namespace CebizPay.Application.Common.Interfaces.Referrals;

/// <summary>
/// Service contract for generating unique, collision-resistant, public-safe referral codes.
/// </summary>
public interface IReferralCodeGenerator
{
    /// <summary>
    /// Generates a random, collision-resistant referral code.
    /// </summary>
    string GenerateCode();
}
