using MediatR;

namespace CebizPay.Application.UseCases.Organizations.RegisterStep2;

/// <summary>
/// Command for KYB Step 2 Organization registration.
/// </summary>
/// <param name="OrganizationId">Target organization ID.</param>
/// <param name="CacNumber">Optional CAC registration number (if already saved via CAC lookup).</param>
/// <param name="LogoUrl">Optional company logo URL (if already uploaded).</param>
/// <param name="CacCertificateUrl">Optional CAC certificate URL (if already uploaded).</param>
public sealed record RegisterStep2Command(
    Guid OrganizationId,
    string? CacNumber = null,
    string? LogoUrl = null,
    string? CacCertificateUrl = null) : IRequest<RegisterStep2ResponseDto>;

/// <summary>
/// Response DTO for RegisterStep2.
/// </summary>
/// <param name="OrganizationId">Organization ID.</param>
/// <param name="CacNumber">CAC number.</param>
/// <param name="Status">Organization status.</param>
/// <param name="KybStatus">KYB status.</param>
public sealed record RegisterStep2ResponseDto(
    Guid OrganizationId,
    string CacNumber,
    string Status,
    string KybStatus);
