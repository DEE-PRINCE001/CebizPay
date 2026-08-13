using CebizPay.Domain.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.UpdateStatus;

/// <summary>
/// Command to update organization status (Verified, Rejected, Suspended).
/// </summary>
/// <param name="OrganizationId">Organization ID.</param>
/// <param name="NewStatus">New lifecycle status.</param>
/// <param name="Reason">Optional reason for status change.</param>
public sealed record UpdateOrganizationStatusCommand(
    Guid OrganizationId,
    OrganizationStatus NewStatus,
    string? Reason) : IRequest<UpdateOrganizationStatusResponseDto>;

/// <summary>
/// Response DTO for organization status update.
/// </summary>
/// <param name="OrganizationId">Organization ID.</param>
/// <param name="Status">New Organization status.</param>
/// <param name="KybStatus">New KYB status.</param>
public sealed record UpdateOrganizationStatusResponseDto(
    Guid OrganizationId,
    string Status,
    string KybStatus);
