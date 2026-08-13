using MediatR;

namespace CebizPay.Application.UseCases.Auth.RegisterPhone;

/// <summary>
/// Command for mobile phone registration initiation via OTP.
/// </summary>
/// <param name="Phone">Target mobile phone number.</param>
/// <param name="DeviceId">Unique device identifier for rate limiting.</param>
public sealed record RegisterPhoneCommand(
    string Phone,
    string DeviceId) : IRequest<RegisterPhoneResponseDto>;

/// <summary>
/// Response DTO for phone registration initiation.
/// </summary>
/// <param name="Success">Indicates whether OTP was successfully generated and sent.</param>
/// <param name="Message">Status or error message.</param>
/// <param name="OtpCode">Development OTP code (if in dev mode).</param>
public sealed record RegisterPhoneResponseDto(
    bool Success,
    string Message,
    string? OtpCode = null);
