using MediatR;

namespace CebizPay.Application.UseCases.Organizations.RegisterStep1;

/// <summary>
/// Command for KYB Step 1 Organization registration.
/// </summary>
/// <param name="CompanyName">Company name.</param>
/// <param name="Email">Company email.</param>
/// <param name="Phone">Company phone.</param>
/// <param name="OwnerUserId">ID of user initiating organization registration.</param>
public sealed record RegisterStep1Command(
    string CompanyName,
    string Email,
    string Phone,
    string OwnerUserId) : IRequest<RegisterStep1ResponseDto>;

/// <summary>
/// Response DTO for RegisterStep1.
/// </summary>
/// <param name="OrganizationId">Created organization ID.</param>
/// <param name="CompanyName">Company name.</param>
/// <param name="Status">Organization status.</param>
/// <param name="KybStatus">KYB status.</param>
public sealed record RegisterStep1ResponseDto(
    Guid OrganizationId,
    string CompanyName,
    string Status,
    string KybStatus);
