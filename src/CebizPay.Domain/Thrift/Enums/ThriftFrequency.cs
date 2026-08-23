namespace CebizPay.Domain.Thrift.Enums;

/// <summary>
/// Contribution and payout rotation frequency for a thrift group.
/// </summary>
public enum ThriftFrequency
{
    /// <summary>Daily thrift cycle.</summary>
    Daily = 1,

    /// <summary>Weekly thrift cycle.</summary>
    Weekly = 2,

    /// <summary>Monthly thrift cycle.</summary>
    Monthly = 3
}
