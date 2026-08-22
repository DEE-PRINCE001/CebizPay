namespace CebizPay.Domain.Loans.Enums;

/// <summary>
/// Status of an active or concluded loan obligation contract.
/// </summary>
public enum LoanContractStatus
{
    /// <summary>Active loan contract with outstanding repayment installments.</summary>
    Active = 1,
    /// <summary>All principal and interest fully settled and paid off.</summary>
    PaidOff = 2,
    /// <summary>One or more installments past due date without settlement.</summary>
    Overdue = 3,
    /// <summary>Contract defaulted following policy determination.</summary>
    Defaulted = 4,
    /// <summary>Corporate payroll loan converted to a standard individual loan following staff termination.</summary>
    ConvertedToIndividual = 5,
    /// <summary>Contract cancelled prior to disbursement.</summary>
    Cancelled = 6
}
