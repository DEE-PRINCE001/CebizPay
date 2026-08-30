namespace CebizPay.Domain.Payments.Enums;

/// <summary>
/// Supported external payment service providers in CebizPay.
/// </summary>
public enum PaymentProvider
{
    /// <summary>Flutterwave payment gateway.</summary>
    Flutterwave = 1,

    /// <summary>Paystack payment gateway.</summary>
    Paystack = 2,

    /// <summary>Monnify payment gateway / BaaS provider.</summary>
    Monnify = 3
}
