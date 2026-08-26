#pragma warning disable CS1591
using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Organizations.Erp;
using CebizPay.Domain.Erp.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// API controller for ERP Company Disbursement Vouchers.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/org/company-vouchers")]
[Authorize]
public sealed class OrgCompanyVouchersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentOrganizationContext _orgContext;

    public OrgCompanyVouchersController(ISender sender, ICurrentOrganizationContext orgContext)
    {
        _sender = sender;
        _orgContext = orgContext;
    }

    private Guid GetOrganizationId()
    {
        var orgId = _orgContext.CurrentOrganizationId;
        if (!orgId.HasValue || orgId.Value == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Active organization context is required.");
        }
        return orgId.Value;
    }

    /// <summary>Creates a new draft company voucher.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateCompanyVoucher([FromBody] CreateCompanyVoucherApiRequest request, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var command = new CreateCompanyVoucherCommand(
            orgId,
            request.PayeeName,
            request.Purpose,
            request.Amount,
            request.Currency,
            request.PaymentMethod,
            request.PayeeDetails,
            request.Notes,
            request.Reference);

        var id = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetCompanyVoucherById), new { id }, id);
    }

    /// <summary>Retrieves paged company vouchers for the active organization.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CompanyVoucherDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCompanyVouchers(
        [FromQuery] CompanyVoucherStatus? status,
        [FromQuery] string? search,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var query = new GetCompanyVouchersQuery(orgId, status, search, fromUtc, toUtc, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Retrieves company voucher details by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CompanyVoucherDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCompanyVoucherById(Guid id, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var query = new GetCompanyVoucherByIdQuery(orgId, id);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Approves a draft company voucher.</summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveCompanyVoucher(Guid id, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        await _sender.Send(new ApproveCompanyVoucherCommand(orgId, id), cancellationToken);
        return Ok();
    }

    /// <summary>Pays or settles an approved company voucher.</summary>
    [HttpPost("{id:guid}/pay")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> PayCompanyVoucher(
        Guid id,
        [FromBody] PayCompanyVoucherApiRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var command = new PayCompanyVoucherCommand(
            orgId,
            id,
            request.PaymentMethod,
            request.Pin,
            request.IdempotencyKey,
            request.Reference);

        await _sender.Send(command, cancellationToken);
        return Ok();
    }

    /// <summary>Cancels a company voucher.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelCompanyVoucher(Guid id, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        await _sender.Send(new CancelCompanyVoucherCommand(orgId, id), cancellationToken);
        return Ok();
    }
}
