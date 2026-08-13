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

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Authentication endpoints.
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
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
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
    /// </summary>
    [HttpPost("mfa/verify")]
    [AllowAnonymous]
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
    /// </summary>
    [HttpPost("mfa/toggle")]
    [Authorize]
    public async Task<IActionResult> ToggleMfa([FromBody] ToggleMfaCommand command, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(command, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Initiates phone registration via OTP.
    /// </summary>
    [HttpPost("register/phone")]
    [AllowAnonymous]
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
    /// </summary>
    [HttpPost("register/otp/verify")]
    [AllowAnonymous]
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
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(command, cancellationToken);
        if (!response.Succeeded)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }
}
