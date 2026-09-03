namespace CebizPay.Application.Common.Interfaces.Support;

/// <summary>
/// Service contract for generating unique, customer-facing support ticket tracking numbers.
/// </summary>
public interface ISupportTicketNumberGenerator
{
    /// <summary>
    /// Generates a unique, collision-resistant ticket number.
    /// </summary>
    string GenerateTicketNumber();
}
