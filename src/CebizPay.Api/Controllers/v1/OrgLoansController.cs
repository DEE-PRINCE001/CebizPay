using System.Security.Claims;
using CebizPay.Application.Common.Interfaces.Loans;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// API controller for organization-level loan administration: reviewing applications, approving with wallet disbursement,
/// declining, and converting loans upon staff offboarding.
/// </summary>
[ApiController]
[Route("api/v1/org/loans")]
[Authorize]
public sealed class OrgLoansController : ControllerBase
{
    private readonly ILoanApplicationService _applicationService;
    private readonly ILoanContractService _contractService;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrgLoansController"/> class.
    /// </summary>
    public OrgLoansController(
        ILoanApplicationService applicationService,
        ILoanContractService contractService,
        ICurrentOrganizationContext orgContext,
        ICurrentUserService currentUserService)
    {
        _applicationService = applicationService;
        _contractService = contractService;
        _orgContext = orgContext;
        _currentUserService = currentUserService;
    }

    private Guid GetOrganizationId()
    {
        return _orgContext.CurrentOrganizationId
            ?? throw new UnauthorizedAccessException("Organization context is missing from request. Provide a valid 'X-Organization-Id' header.");
    }

    private string GetUserId()
    {
        return _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("User authentication context is required.");
    }

    /// <summary>
    /// Lists all staff loan applications for the organization.
    /// </summary>
    [HttpGet("applications")]
    [ProducesResponseType(typeof(IReadOnlyList<LoanApplicationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetApplications(CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.LoanView, cancellationToken))
        {
            return Forbid();
        }

        var apps = await _applicationService.GetApplicationsForOrgAsync(orgId, cancellationToken);
        return Ok(apps);
    }

    /// <summary>
    /// Gets a single staff loan application by ID.
    /// </summary>
    [HttpGet("applications/{id:guid}")]
    [ProducesResponseType(typeof(LoanApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetApplicationById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.LoanView, cancellationToken))
        {
            return Forbid();
        }

        var app = await _applicationService.GetApplicationByIdAsync(orgId, id, null, cancellationToken);
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
    /// Formally approves a staff loan application, creates loan contract, builds repayment schedule,
    /// and issues atomic wallet principal disbursement. Self-approval is strictly prevented.
    /// </summary>
    [HttpPost("applications/{id:guid}/approve")]
    [ProducesResponseType(typeof(LoanContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ApproveApplication(
        Guid id,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.LoanApprove, cancellationToken))
        {
            return Forbid();
        }

        var approverUserId = GetUserId();

        var contract = await _applicationService.ApproveApplicationAsync(orgId, id, approverUserId, cancellationToken);
        return Ok(contract);
    }

    /// <summary>
    /// Formally declines a staff loan application with recorded rationale.
    /// </summary>
    [HttpPost("applications/{id:guid}/decline")]
    [ProducesResponseType(typeof(LoanApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeclineApplication(
        Guid id,
        [FromBody] DeclineLoanApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.LoanDecide, cancellationToken))
        {
            return Forbid();
        }

        var deciderUserId = GetUserId();

        var app = await _applicationService.DeclineApplicationAsync(orgId, id, deciderUserId, request.Reason, cancellationToken);
        return Ok(app);
    }

    /// <summary>
    /// Lists all active and concluded loan contracts for the organization.
    /// </summary>
    [HttpGet("contracts")]
    [ProducesResponseType(typeof(IReadOnlyList<LoanContractDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetContracts(CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.LoanView, cancellationToken))
        {
            return Forbid();
        }

        var contracts = await _contractService.GetContractsForOrgAsync(orgId, cancellationToken);
        return Ok(contracts);
    }

    /// <summary>
    /// Gets a single loan contract with its repayment schedule by ID.
    /// </summary>
    [HttpGet("contracts/{id:guid}")]
    [ProducesResponseType(typeof(LoanContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetContractById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.LoanView, cancellationToken))
        {
            return Forbid();
        }

        var contract = await _contractService.GetContractByIdAsync(orgId, id, null, cancellationToken);
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

    /// <summary>
    /// Converts outstanding corporate payroll loans for a departing/terminated staff member into standard individual loans.
    /// </summary>
    [HttpPost("staff/{staffUserId}/convert-offboarding")]
    [ProducesResponseType(typeof(IReadOnlyList<LoanContractDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConvertTerminatedStaffLoans(
        string staffUserId,
        [FromBody] ConvertStaffLoansRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.LoanDecide, cancellationToken))
        {
            return Forbid();
        }

        var actorUserId = GetUserId();

        var converted = await _contractService.ConvertTerminatedStaffLoansAsync(
            orgId, staffUserId, request.Reason, actorUserId, cancellationToken);
        return Ok(converted);
    }
}
