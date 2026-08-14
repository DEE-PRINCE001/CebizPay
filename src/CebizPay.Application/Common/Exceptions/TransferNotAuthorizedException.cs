namespace CebizPay.Application.Common.Exceptions;

/// <summary>
/// Thrown when the authenticated user is not authorized to execute an outbound transfer.
/// For example: accessing a wallet they don't own, or lacking the Wallet.Transfer permission.
/// Maps to HTTP 403 Forbidden with code TRANSFER_NOT_AUTHORIZED.
/// </summary>
public sealed class TransferNotAuthorizedException : Exception
{
    /// <summary>Stable machine-readable error code.</summary>
    public string Code { get; } = "TRANSFER_NOT_AUTHORIZED";

    /// <summary>
    /// Initializes a new instance of <see cref="TransferNotAuthorizedException"/>.
    /// </summary>
    public TransferNotAuthorizedException(string reason)
        : base(reason)
    {
    }
}
