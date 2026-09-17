namespace CebizPay.Application.UseCases.Organizations.Profile;

/// <summary>
/// Authoritative corporate profile and verification details of an organization tenant.
/// </summary>
public sealed record OrgProfileDto(
    Guid OrganizationId,
    string Name,
    string Email,
    string? PhoneNumber,
    string? Address,
    string? Category,
    string Status,
    string? LogoUrl,
    string? CacNumber,
    string? CacCertificateUrl,
    DateTime RegisteredAtUtc,
    string? PhotoUrl = null);
