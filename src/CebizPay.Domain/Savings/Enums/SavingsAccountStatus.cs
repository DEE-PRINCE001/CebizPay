namespace CebizPay.Domain.Savings.Enums;

/// <summary>
/// Status of a savings account / instance.
/// </summary>
public enum SavingsAccountStatus
{
    /// <summary>Account opened and awaiting initial contribution or activation.</summary>
    Pending = 1,

    /// <summary>Active savings account accruing interest and/or receiving recurring contributions.</summary>
    Active = 2,

    /// <summary>Account has reached term maturity and is available for full principal and accrued interest withdrawal.</summary>
    Matured = 3,

    /// <summary>Funds fully liquidated and settled to owner's wallet.</summary>
    Withdrawn = 4,

    /// <summary>Account cancelled prior to activation or contribution.</summary>
    Cancelled = 5
}
