namespace CebizPay.Application.Common.Interfaces.Compliance;

/// <summary>
/// Configuration and session parameters for initializing the Dojah KYC client-side widget.
/// </summary>
public sealed record DojahWidgetConfigDto(
    string AppId,
    string PublicKey,
    string ReferenceId,
    string WidgetType,
    DojahWidgetUserDataDto UserData,
    IReadOnlyList<string> EnabledPages);

/// <summary>
/// User demographic details to pre-populate in the Dojah KYC widget.
/// </summary>
public sealed record DojahWidgetUserDataDto(
    string? FirstName,
    string? LastName,
    string? Email,
    string? Phone);
