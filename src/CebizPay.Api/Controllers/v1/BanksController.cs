using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Financial institutions directory endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/banks")]
[Authorize]
public sealed class BanksController : ControllerBase
{
    private readonly IBankDirectoryService _bankDirectoryService;

    /// <summary>
    /// Initializes a new instance of <see cref="BanksController"/>.
    /// </summary>
    public BanksController(IBankDirectoryService bankDirectoryService)
    {
        _bankDirectoryService = bankDirectoryService ?? throw new ArgumentNullException(nameof(bankDirectoryService));
    }

    /// <summary>
    /// Retrieves the directory of commercial and digital banks supported for outbound transfers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetBanks(CancellationToken cancellationToken)
    {
        var banks = await _bankDirectoryService.GetBanksAsync(cancellationToken);
        return Ok(new
        {
            success = true,
            data = banks
        });
    }
}
