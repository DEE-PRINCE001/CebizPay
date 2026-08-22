using CebizPay.Domain.Loans.Entities;
using CebizPay.Domain.Loans.Enums;

namespace CebizPay.Domain.Loans.Events;

/// <summary>Event emitted when a corporate loan plan is created.</summary>
public sealed record LoanPlanCreatedDomainEvent(CorporateLoanPlan Plan) : IDomainEvent;

/// <summary>Event emitted when a corporate loan plan is updated.</summary>
public sealed record LoanPlanUpdatedDomainEvent(CorporateLoanPlan Plan) : IDomainEvent;

/// <summary>Event emitted when a staff loan application is submitted.</summary>
public sealed record LoanApplicationSubmittedDomainEvent(LoanApplication Application) : IDomainEvent;

/// <summary>Event emitted when a staff loan application is approved.</summary>
public sealed record LoanApplicationApprovedDomainEvent(LoanApplication Application, Guid ContractId) : IDomainEvent;

/// <summary>Event emitted when a staff loan application is declined.</summary>
public sealed record LoanApplicationDeclinedDomainEvent(LoanApplication Application, string Reason) : IDomainEvent;

/// <summary>Event emitted when a loan contract is issued.</summary>
public sealed record LoanContractCreatedDomainEvent(LoanContract Contract) : IDomainEvent;

/// <summary>Event emitted when loan principal funds are disbursed to borrower wallet.</summary>
public sealed record LoanDisbursedDomainEvent(LoanContract Contract, Guid LedgerTransactionId) : IDomainEvent;

/// <summary>Event emitted when a loan repayment installment is settled.</summary>
public sealed record LoanRepaymentPaidDomainEvent(LoanContract Contract, LoanRepaymentScheduleItem InstallmentItem) : IDomainEvent;

/// <summary>Event emitted when a loan repayment installment is missed/overdue.</summary>
public sealed record LoanRepaymentMissedDomainEvent(LoanContract Contract, LoanRepaymentScheduleItem InstallmentItem) : IDomainEvent;

/// <summary>Event emitted when a corporate payroll loan is converted to a standard individual loan upon staff offboarding.</summary>
public sealed record LoanConvertedToIndividualDomainEvent(LoanContract OriginalLoan, LoanContract ConvertedLoan) : IDomainEvent;

/// <summary>Marker interface for domain events.</summary>
public interface IDomainEvent { }
