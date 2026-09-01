using CebizPay.Application.Common.Interfaces.Compliance;

namespace CebizPay.Infrastructure.Compliance.Ninja;

/// <summary>
/// HTTP client contract for Ninja compliance verification APIs.
/// </summary>
public interface INinjaClient
{
    /// <summary>Resolves and verifies BVN against official registry.</summary>
    Task<VerificationProviderResult> VerifyBvnAsync(
        string bvn,
        string firstName,
        string lastName,
        DateTime? dateOfBirth = null,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves and verifies NIN against official registry.</summary>
    Task<VerificationProviderResult> VerifyNinAsync(
        string nin,
        string firstName,
        string lastName,
        DateTime? dateOfBirth = null,
        CancellationToken cancellationToken = default);

    /// <summary>Verifies CAC business registry records.</summary>
    Task<VerificationProviderResult> VerifyCacAsync(
        string rcNumber,
        string companyName,
        CancellationToken cancellationToken = default);

    /// <summary>Screens against AML and sanctions watchlists.</summary>
    Task<VerificationProviderResult> ScreenAmlAsync(
        string name,
        bool isEntity = false,
        CancellationToken cancellationToken = default);
}
