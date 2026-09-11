namespace CebizPay.Application.UseCases.Admin.Organizations;

/// <summary>
/// Summary DTO for an organization in the platform directory.
/// </summary>
public sealed record AdminOrganizationSummaryDto(
    Guid Id,
    string Name,
    string? Category,
    string Email,
    string? Address,
    string Status,
    int StaffCount,
    string? LogoUrl,
    DateTime CreatedAt);

/// <summary>
/// Credential / KYB verification document representation.
/// </summary>
public sealed record AdminOrganizationCredentialDto(
    string Id,
    string Title,
    string DocumentType,
    string FileUrl,
    DateTime? UploadedAt);

/// <summary>
/// Detailed operational and KYB profile for a specific organization.
/// </summary>
public sealed record AdminOrganizationDetailsDto(
    Guid Id,
    string Name,
    string? Category,
    string Email,
    string? Address,
    string Status,
    int StaffCount,
    string? LogoUrl,
    string? PhotoUrl,
    DateTime RegisteredAt,
    IReadOnlyList<AdminOrganizationCredentialDto> Credentials);

/// <summary>
/// Staff member roster item viewed in platform admin scope.
/// </summary>
public sealed record AdminStaffRosterItemDto(
    string Id,
    string Name,
    string? WalletId,
    string? BankAccount,
    string? Email,
    string? MonthlySalary,
    string Status,
    string? AvatarUrl);

/// <summary>
/// Verification document item for an organization.
/// </summary>
public sealed record AdminOrganizationDocumentDto(
    string Id,
    string Title,
    string DocumentType,
    string FileUrl,
    long? FileSizeBytes,
    DateTime? UploadedAt);

/// <summary>
/// Response envelope for organization verification documents.
/// </summary>
public sealed record AdminOrganizationDocumentsResponseDto(
    Guid OrganizationId,
    IReadOnlyList<AdminOrganizationDocumentDto> Documents);

/// <summary>
/// Result of exporting organizations data.
/// </summary>
public sealed record ExportAdminOrganizationsResult(
    byte[] Content,
    string ContentType,
    string FileName);
