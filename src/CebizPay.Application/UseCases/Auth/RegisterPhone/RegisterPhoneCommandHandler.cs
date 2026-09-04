using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Utils;
using MediatR;

namespace CebizPay.Application.UseCases.Auth.RegisterPhone;

/// <summary>
/// Handler for RegisterPhoneCommand.
/// </summary>
public sealed class RegisterPhoneCommandHandler : IRequestHandler<RegisterPhoneCommand, RegisterPhoneResponseDto>
{
    private readonly IOtpService _otpService;

    /// <summary>
    /// Initializes a new instance of <see cref="RegisterPhoneCommandHandler"/>.
    /// </summary>
    public RegisterPhoneCommandHandler(IOtpService otpService)
    {
        _otpService = otpService;
    }

    /// <inheritdoc/>
    public async Task<RegisterPhoneResponseDto> Handle(RegisterPhoneCommand request, CancellationToken cancellationToken)
    {
        var canonicalPhone = PhoneNormalizer.NormalizeE164(request.Phone);
        var result = await _otpService.RequestOtpAsync(canonicalPhone, request.DeviceId, cancellationToken);
        if (!result.Success)
        {
            return new RegisterPhoneResponseDto(false, result.Error ?? "OTP generation failed.");
        }

        return new RegisterPhoneResponseDto(true, "OTP sent successfully.", result.Code);
    }
}
