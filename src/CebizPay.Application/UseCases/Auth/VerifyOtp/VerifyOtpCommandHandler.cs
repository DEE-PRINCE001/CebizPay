using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Utils;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Events;
using MediatR;

namespace CebizPay.Application.UseCases.Auth.VerifyOtp;

/// <summary>
/// Handler for executing VerifyOtpCommand.
/// </summary>
public sealed class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, VerifyOtpResponseDto>
{
    private static readonly string[] OtpErrorMessages = ["Invalid or expired OTP code."];

    private readonly IOtpService _otpService;
    private readonly IIdentityService _identityService;
    private readonly IApplicationDbContext _dbContext;
    private readonly IEventPublisher _eventPublisher;

    /// <summary>
    /// Initializes a new instance of <see cref="VerifyOtpCommandHandler"/>.
    /// </summary>
    public VerifyOtpCommandHandler(
        IOtpService otpService,
        IIdentityService identityService,
        IApplicationDbContext dbContext,
        IEventPublisher eventPublisher)
    {
        _otpService = otpService;
        _identityService = identityService;
        _dbContext = dbContext;
        _eventPublisher = eventPublisher;
    }

    /// <inheritdoc/>
    public async Task<VerifyOtpResponseDto> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        var canonicalPhone = PhoneNormalizer.NormalizeE164(request.Phone);
        var isOtpValid = await _otpService.VerifyOtpAsync(canonicalPhone, request.Code, cancellationToken);
        if (!isOtpValid)
        {
            return new VerifyOtpResponseDto(false, null, null, null, OtpErrorMessages);
        }

        var regResult = await _identityService.RegisterUserAsync(
            request.Email, request.Password, canonicalPhone, cancellationToken);

        if (!regResult.Succeeded)
        {
            return new VerifyOtpResponseDto(false, null, null, null, regResult.Errors);
        }

        var profile = new IndividualProfile(regResult.UserId, request.FirstName, request.LastName);
        _dbContext.IndividualProfiles.Add(profile);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _eventPublisher.PublishAsync(
            new UserRegisteredDomainEvent(regResult.UserId, request.Email, canonicalPhone, DateTime.UtcNow),
            cancellationToken);

        var loginResult = await _identityService.LoginAsync(request.Email, request.Password, cancellationToken);

        return new VerifyOtpResponseDto(
            true, regResult.UserId, loginResult.AccessToken, loginResult.RefreshToken, null);
    }
}
