using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Erp.Entities;
using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Erp.Events;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Erp;

/// <summary>
/// Command to create a new organization customer.
/// </summary>
public sealed record CreateCustomerCommand(
    Guid OrganizationId,
    string Reference,
    string Name,
    string? Email = null,
    string? Phone = null,
    string? Address = null) : IRequest<Guid>;

/// <summary>
/// Validator for CreateCustomerCommand.
/// </summary>
public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    /// <summary>
    /// Initializes validation rules for CreateCustomerCommand.
    /// </summary>
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.Reference).NotEmpty().WithMessage("Customer reference is required.").MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Customer name is required.").MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("Invalid email address.");
    }
}

/// <summary>
/// Handler for CreateCustomerCommand.
/// </summary>
public sealed class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="CreateCustomerCommandHandler"/>.
    /// </summary>
    public CreateCustomerCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext,
        ICurrentUserService currentUserService,
        IOutboxService outboxService)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
        _currentUserService = currentUserService;
        _outboxService = outboxService;
    }

    /// <inheritdoc/>
    public async Task<Guid> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var org = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Organization {request.OrganizationId} not found.");

        if (!org.CanConfigureHris())
        {
            throw new InvalidOperationException("Cannot create customers while organization status is suspended.");
        }

        var normalizedRef = request.Reference.Trim().ToUpperInvariant();
        var refExists = await _dbContext.Customers.AnyAsync(
            c => c.OrganizationId == request.OrganizationId && c.Reference == normalizedRef && !c.IsDeleted,
            cancellationToken);

        if (refExists)
        {
            throw new InvalidOperationException($"A customer with reference '{normalizedRef}' already exists in this organization.");
        }

        var customer = new Customer(
            request.OrganizationId,
            normalizedRef,
            request.Name,
            request.Email,
            request.Phone,
            request.Address);

        _dbContext.Customers.Add(customer);

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.CustomerCreated,
            resourceType: AuditResourceTypes.Customer,
            resourceId: customer.Id.ToString(),
            organizationId: request.OrganizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                customer.Id,
                customer.Reference,
                customer.Name,
                customer.Email,
                customer.Phone,
                customer.Address
            }));
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new CustomerCreatedDomainEvent(
            customer.Id,
            request.OrganizationId,
            customer.Reference,
            customer.Name,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return customer.Id;
    }
}

/// <summary>
/// Command to update an existing customer.
/// </summary>
public sealed record UpdateCustomerCommand(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Email = null,
    string? Phone = null,
    string? Address = null) : IRequest<Guid>;

/// <summary>
/// Validator for UpdateCustomerCommand.
/// </summary>
public sealed class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    /// <summary>
    /// Initializes validation rules for UpdateCustomerCommand.
    /// </summary>
    public UpdateCustomerCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Customer ID is required.");
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Customer name is required.").MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("Invalid email address.");
    }
}

/// <summary>
/// Handler for UpdateCustomerCommand.
/// </summary>
public sealed class UpdateCustomerCommandHandler : IRequestHandler<UpdateCustomerCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="UpdateCustomerCommandHandler"/>.
    /// </summary>
    public UpdateCustomerCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext,
        ICurrentUserService currentUserService,
        IOutboxService outboxService)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
        _currentUserService = currentUserService;
        _outboxService = outboxService;
    }

    /// <inheritdoc/>
    public async Task<Guid> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var org = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Organization {request.OrganizationId} not found.");

        if (!org.CanConfigureHris())
        {
            throw new InvalidOperationException("Cannot update customers while organization status is suspended.");
        }

        var customer = await _dbContext.Customers.FirstOrDefaultAsync(
            c => c.Id == request.Id && c.OrganizationId == request.OrganizationId && !c.IsDeleted,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Customer '{request.Id}' was not found in this organization.");

        var beforeJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            customer.Id,
            customer.Name,
            customer.Email,
            customer.Phone,
            customer.Address
        });

        customer.Update(
            request.Name,
            request.Email,
            request.Phone,
            request.Address);

        var afterJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            customer.Id,
            customer.Name,
            customer.Email,
            customer.Phone,
            customer.Address
        });

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.CustomerUpdated,
            resourceType: AuditResourceTypes.Customer,
            resourceId: customer.Id.ToString(),
            organizationId: request.OrganizationId,
            beforeJson: beforeJson,
            afterJson: afterJson);
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new CustomerUpdatedDomainEvent(
            customer.Id,
            request.OrganizationId,
            customer.Reference,
            customer.Name,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return customer.Id;
    }
}

/// <summary>
/// Command to soft-delete / deactivate a customer.
/// </summary>
public sealed record DeleteCustomerCommand(Guid Id, Guid OrganizationId) : IRequest<bool>;

/// <summary>
/// Handler for DeleteCustomerCommand.
/// </summary>
public sealed class DeleteCustomerCommandHandler : IRequestHandler<DeleteCustomerCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="DeleteCustomerCommandHandler"/>.
    /// </summary>
    public DeleteCustomerCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext,
        ICurrentUserService currentUserService,
        IOutboxService outboxService)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
        _currentUserService = currentUserService;
        _outboxService = outboxService;
    }

    /// <inheritdoc/>
    public async Task<bool> Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var org = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Organization {request.OrganizationId} not found.");

        if (!org.CanConfigureHris())
        {
            throw new InvalidOperationException("Cannot delete customers while organization status is suspended.");
        }

        var customer = await _dbContext.Customers.FirstOrDefaultAsync(
            c => c.Id == request.Id && c.OrganizationId == request.OrganizationId && !c.IsDeleted,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Customer '{request.Id}' was not found in this organization.");

        customer.SoftDelete();

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.CustomerDeleted,
            resourceType: AuditResourceTypes.Customer,
            resourceId: customer.Id.ToString(),
            organizationId: request.OrganizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                customer.Id,
                customer.Reference,
                customer.Status,
                customer.IsDeleted
            }));
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new CustomerDeletedDomainEvent(
            customer.Id,
            request.OrganizationId,
            customer.Reference,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}

/// <summary>
/// Query to list customers with search and pagination.
/// </summary>
public sealed record GetCustomersQuery(
    Guid OrganizationId,
    string? SearchTerm = null,
    CustomerStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<CustomerDto>>;

/// <summary>
/// Validator for GetCustomersQuery.
/// </summary>
public sealed class GetCustomersQueryValidator : AbstractValidator<GetCustomersQuery>
{
    /// <summary>
    /// Initializes validation rules for GetCustomersQuery.
    /// </summary>
    public GetCustomersQueryValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1).WithMessage("PageNumber must be at least 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for GetCustomersQuery.
/// </summary>
public sealed class GetCustomersQueryHandler : IRequestHandler<GetCustomersQuery, PagedResult<CustomerDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetCustomersQueryHandler"/>.
    /// </summary>
    public GetCustomersQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    /// <inheritdoc/>
    public async Task<PagedResult<CustomerDto>> Handle(GetCustomersQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var query = _dbContext.Customers.Where(c => c.OrganizationId == request.OrganizationId && !c.IsDeleted);

        if (request.Status.HasValue)
        {
            query = query.Where(c => c.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var search = request.SearchTerm.Trim().ToLowerInvariant();
#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(c => c.Reference.ToLower().Contains(search) || c.Name.ToLower().Contains(search) || (c.Email != null && c.Email.ToLower().Contains(search)));
#pragma warning restore CA1862, CA1304, CA1311
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var customers = await query
            .OrderBy(c => c.Name)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = customers.Select(c => new CustomerDto(
            c.Id,
            c.OrganizationId,
            c.Reference,
            c.Name,
            c.Email,
            c.Phone,
            c.Address,
            c.Status,
            c.CreatedAtUtc,
            c.UpdatedAtUtc)).ToList();

        return new PagedResult<CustomerDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}

/// <summary>
/// Query to get single customer details.
/// </summary>
public sealed record GetCustomerByIdQuery(Guid Id, Guid OrganizationId) : IRequest<CustomerDto?>;

/// <summary>
/// Handler for GetCustomerByIdQuery.
/// </summary>
public sealed class GetCustomerByIdQueryHandler : IRequestHandler<GetCustomerByIdQuery, CustomerDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetCustomerByIdQueryHandler"/>.
    /// </summary>
    public GetCustomerByIdQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    /// <inheritdoc/>
    public async Task<CustomerDto?> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var customer = await _dbContext.Customers.FirstOrDefaultAsync(
            c => c.Id == request.Id && c.OrganizationId == request.OrganizationId && !c.IsDeleted,
            cancellationToken);

        if (customer == null)
        {
            return null;
        }

        return new CustomerDto(
            customer.Id,
            customer.OrganizationId,
            customer.Reference,
            customer.Name,
            customer.Email,
            customer.Phone,
            customer.Address,
            customer.Status,
            customer.CreatedAtUtc,
            customer.UpdatedAtUtc);
    }
}
