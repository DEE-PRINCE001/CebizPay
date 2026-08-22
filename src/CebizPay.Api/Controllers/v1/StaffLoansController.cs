using System.Security.Claims;
using CebizPay.Application.Common.Interfaces.Loans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// API controller for staff-facing loan operations: pre-submission calculation preview, application submission, and personal loan tracking.
/// </summary>
[ApiController]
[Route("api/v1/work/loans")]
[Authorize]
public sealed class StaffLoansController : ControllerBase
{
    private readonly ILoanApplicationService _applicationService;
    private readonly ILoanContractService _contractService;

    /// <summary>
    /// Initializes a new instance of the <see cref="StaffLoansController"/> class.
    /// </summary>
    public StaffLoansController(
        ILoanApplicationService applicationService,
        ILoanContractService contractService)
    {
        _applicationService = applicationService;
        _contractService = contractService;
    }

    private Guid GetOrganizationId()
    {
        var orgIdClaim = User.FindFirstValue("OrganizationId") ?? User.FindFirstValue("org_id");
        if (string.IsNullOrEmpty(orgIdClaim) || !Guid.TryParse(orgIdClaim, out var orgId))
        {
            throw new UnauthorizedAccessException("Organization context is missing from token.");
        }
        return orgId;
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("User ID is missing from token.");
    }

    /// <summary>
    /// Computes a dry-run preview of loan terms, monthly installments, total repayment, and 33% DTI ratio before submission.
    /// </summary>
    [HttpPost("preview")]
    [ProducesResponseType(typeof(LoanCalculationPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewLoan(
        [FromBody] LoanCalculationPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var userId = GetUserId();

        var preview = await _applicationService.PreviewApplicationAsync(orgId, userId, request, cancellationToken);
        return Ok(preview);
    }

    /// <summary>
    /// Submits a staff loan application.
    /// </summary>
    [HttpPost("applications")]
    [ProducesResponseType(typeof(LoanApplicationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitApplication(
        [FromBody] SubmitLoanApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var userId = GetUserId();

        var application = await _applicationService.SubmitApplicationAsync(orgId, userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetApplicationById), new { id = application.Id }, application);
    }

    /// <summary>
    /// Gets a single submitted loan application by ID.
    /// </summary>
    [HttpGet("applications/{id:guid}")]
    [ProducesResponseType(typeof(LoanApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetApplicationById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var userId = GetUserId();

        var app = await _applicationService.GetApplicationByIdAsync(orgId, id, userId, cancellationToken);
        if (app == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Application Not Found",
                Detail = $"Loan application '{id}' was not found."
            });
        }
        return Ok(app);
    }

    /// <summary>
    /// Lists all loan applications submitted by the authenticated staff member.
    /// </summary>
    [HttpGet("applications")]
    [ProducesResponseType(typeof(IReadOnlyList<LoanApplicationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyApplications(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var apps = await _applicationService.GetApplicationsForUserAsync(userId, cancellationToken);
        return Ok(apps);
    }

    /// <summary>
    /// Lists all active and past loan contracts for the authenticated staff member.
    /// </summary>
    [HttpGet("contracts")]
    [ProducesResponseType(typeof(IReadOnlyList<LoanContractDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyContracts(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var contracts = await _contractService.GetContractsForUserAsync(userId, cancellationToken);
        return Ok(contracts);
    }

    /// <summary>
    /// Gets a single loan contract with its repayment schedule by ID.
    /// </summary>
    [HttpGet("contracts/{id:guid}")]
    [ProducesResponseType(typeof(LoanContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContractById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var userId = GetUserId();

        var contract = await _contractService.GetContractByIdAsync(orgId, id, userId, cancellationToken);
        if (contract == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Contract Not Found",
                Detail = $"Loan contract '{id}' was not found."
            });
        }
        return Ok(contract);
    }
}
