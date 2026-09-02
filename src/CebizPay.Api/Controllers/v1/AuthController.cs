using Asp.Versioning;
using CebizPay.Application.UseCases.Auth.ChangePassword;
using CebizPay.Application.UseCases.Auth.Login;
using CebizPay.Application.UseCases.Auth.RegisterPhone;
using CebizPay.Application.UseCases.Auth.ToggleMfa;
using CebizPay.Application.UseCases.Auth.VerifyMfa;
using CebizPay.Application.UseCases.Auth.VerifyOtp;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Authentication endpoints protected by targeted ASP.NET Core rate limiting policies.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="AuthController"/>.
    /// </summary>
    public AuthController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Authenticates a user with email and password.
    /// Rate limited by AuthLoginPolicy.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthLoginPolicy")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(command, cancellationToken);
        if (!response.Succeeded)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }

    /// <summary>
    /// Verifies short-lived MFA challenge code to obtain JWT tokens.
    /// Rate limited by MfaVerificationPolicy.
    /// </summary>
    [HttpPost("mfa/verify")]
    [AllowAnonymous]
    [EnableRateLimiting("MfaVerificationPolicy")]
    public async Task<IActionResult> VerifyMfa([FromBody] VerifyMfaCommand command, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(command, cancellationToken);
        if (!response.Succeeded)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }

    /// <summary>
    /// Enables or disables MFA for the authenticated user/admin profile.
    /// Rate limited by AuthPolicy.
    /// </summary>
    [HttpPost("mfa/toggle")]
    [Authorize]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<IActionResult> ToggleMfa([FromBody] ToggleMfaCommand command, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(command, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Initiates phone registration via OTP.
    /// Rate limited by OtpRequestPolicy.
    /// </summary>
    [HttpPost("register/phone")]
    [AllowAnonymous]
    [EnableRateLimiting("OtpRequestPolicy")]
    public async Task<IActionResult> RegisterPhone([FromBody] RegisterPhoneCommand command, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(command, cancellationToken);
        if (!response.Success)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }

    /// <summary>
    /// Verifies mobile OTP and completes registration.
    /// Rate limited by OtpVerificationPolicy.
    /// </summary>
    [HttpPost("register/otp/verify")]
    [AllowAnonymous]
    [EnableRateLimiting("OtpVerificationPolicy")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpCommand command, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(command, cancellationToken);
        if (!response.Success)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }

    /// <summary>
    /// Changes password for the authenticated user.
    /// Rate limited by AuthPolicy.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(command, cancellationToken);
        if (!response.Succeeded)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }

    /// <summary>
    /// Redeems an administrative invitation token and initializes admin credentials.
    /// Rate limited by AuthPolicy.
    /// </summary>
    [HttpPost("admin/redeem-invite")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<IActionResult> RedeemAdminInvite([FromBody] CebizPay.Application.UseCases.Admin.Manage.RedeemAdminInviteCommand command, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(command, cancellationToken);
        if (!response.Succeeded)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }

    /// <summary>
    /// Exchanges an active refresh token for a new JWT access token and rotated refresh token.
    /// Rate limited by AuthPolicy.
    /// </summary>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<IActionResult> RefreshToken([FromBody] CebizPay.Application.UseCases.Auth.RefreshToken.RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(command, cancellationToken);
        if (!response.Succeeded)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }

    /// <summary>
    /// Explicitly revokes a refresh token (e.g. upon user logout).
    /// Rate limited by AuthPolicy.
    /// </summary>
    [HttpPost("revoke-token")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<IActionResult> RevokeToken([FromBody] CebizPay.Application.UseCases.Auth.RevokeToken.RevokeTokenCommand command, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(command, cancellationToken);
        if (!response.Succeeded)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }
}
