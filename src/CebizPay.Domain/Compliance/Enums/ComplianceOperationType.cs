namespace CebizPay.Domain.Compliance.Enums;

/// <summary>
/// Financial operation categories evaluated for compliance eligibility.
/// </summary>
public enum ComplianceOperationType
{
    /// <summary>Outbound bank transfer / payout via NIP.</summary>
    BankTransferPayout = 1,
    /// <summary>Inbound card funding.</summary>
    CardFunding = 2,
    /// <summary>Inbound virtual account deposit.</summary>
    VirtualAccountFunding = 3,
    /// <summary>Internal wallet peer-to-peer transfer.</summary>
    PeerTransfer = 4,
    /// <summary>Corporate payroll disbursement.</summary>
    SalaryDisbursement = 5,
    /// <summary>Value-added service / bill payment.</summary>
    VasPurchase = 6
}
