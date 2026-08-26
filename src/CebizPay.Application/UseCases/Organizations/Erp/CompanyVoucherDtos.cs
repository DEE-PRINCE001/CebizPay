#pragma warning disable CS1591
using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Application.UseCases.Organizations.Erp;

/// <summary>DTO representation of a company disbursement voucher.</summary>
public sealed record CompanyVoucherDto(
    Guid Id,
    Guid OrganizationId,
    string VoucherNumber,
    string PayeeName,
    string? PayeeDetails,
    string Purpose,
    decimal Amount,
    Currency Currency,
    CompanyVoucherPaymentMethod PaymentMethod,
    CompanyVoucherStatus Status,
    string CreatedByUserId,
    string? ApprovedByUserId,
    DateTime? ApprovedAtUtc,
    DateTime? PaidAtUtc,
    Guid? WalletId,
    Guid? LedgerTransactionId,
    string? Reference,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Request payload to create a new company disbursement voucher.</summary>
public sealed record CreateCompanyVoucherApiRequest(
    string PayeeName,
    string Purpose,
    decimal Amount,
    Currency Currency = Currency.NGN,
    CompanyVoucherPaymentMethod PaymentMethod = CompanyVoucherPaymentMethod.Manual,
    string? PayeeDetails = null,
    string? Notes = null,
    string? Reference = null);

/// <summary>Request payload to pay/settle an approved company disbursement voucher.</summary>
public sealed record PayCompanyVoucherApiRequest(
    CompanyVoucherPaymentMethod PaymentMethod = CompanyVoucherPaymentMethod.Manual,
    string? Pin = null,
    string? IdempotencyKey = null,
    string? Reference = null);
