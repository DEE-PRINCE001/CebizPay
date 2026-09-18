namespace CebizPay.Application.UseCases.Admin.Individuals;

/// <summary>
/// DTO representing an individual user in the administrative directory listing.
/// </summary>
public sealed record AdminIndividualSummaryDto(
    Guid Id,
    string Name,
    string Email,
    string PhoneNumber,
    string ProfessionalStatus,
    string CompanyName,
    string Status,
    string? AvatarUrl,
    DateTime CreatedAt);

/// <summary>
/// DTO representing a KYC verification credential submitted by an individual.
/// </summary>
public sealed record AdminIndividualCredentialDto(
    string Id,
    string Title,
    string DocumentType,
    string DocumentNumber,
    string FileUrl,
    DateTime UploadedAt);

/// <summary>
/// DTO representing complete administrative profile details for an individual user.
/// </summary>
public sealed record AdminIndividualDetailsDto(
    Guid Id,
    string Name,
    string Email,
    string PhoneNumber,
    string Status,
    string ProfessionalStatus,
    string CompanyName,
    string? PhotoUrl,
    DateTime RegisteredAt,
    IReadOnlyList<AdminIndividualCredentialDto> Credentials,
    bool IsSuspended = false,
    DateTime? SuspendedAtUtc = null,
    string? SuspensionReason = null);

/// <summary>
/// DTO representing a single transaction item in the administrative view for an individual.
/// </summary>
public sealed record AdminIndividualTransactionItemDto(
    string Id,
    string CounterpartyName,
    string? CounterpartyAvatarUrl,
    decimal Amount,
    string TransactionType,
    string ReceiverSenderId,
    string Method,
    string AccountOrWalletId,
    DateTime DateTime,
    string Status);

/// <summary>
/// DTO representing the administrative overview of an individual's wallet.
/// </summary>
public sealed record AdminIndividualWalletDto(
    string WalletId,
    decimal AvailableBalance,
    decimal LedgerBalance,
    string Currency,
    int Tier,
    string VirtualAccountNumber,
    string BankName,
    string Status);

/// <summary>
/// DTO representing a single savings plan instance associated with an individual.
/// </summary>
public sealed record AdminIndividualSavingsItemDto(
    string Id,
    string Name,
    decimal? TargetAmount,
    decimal CurrentAmount,
    string Frequency,
    decimal InterestRate,
    DateTime StartDate,
    DateTime MaturityDate,
    string Status);

/// <summary>
/// DTO envelope representing savings plans list for an individual.
/// </summary>
public sealed record AdminIndividualSavingsListDto(
    IReadOnlyList<AdminIndividualSavingsItemDto> Items,
    int TotalCount);

/// <summary>
/// Result envelope for exported individuals dataset.
/// </summary>
public sealed record ExportAdminIndividualsResult(
    byte[] Content,
    string ContentType,
    string FileName);

/// <summary>
/// DTO representing the result of an administrative individual status update.
/// </summary>
public sealed record AdminIndividualStatusResultDto(
    Guid ProfileId,
    string UserId,
    string Status,
    bool IsSuspended,
    DateTime? SuspendedAtUtc,
    string? SuspensionReason);

/// <summary>
/// Request payload to administratively suspend an individual profile.
/// </summary>
public sealed record SuspendIndividualRequest(string Reason);

/// <summary>
/// Request payload to administratively reactivate an individual profile.
/// </summary>
public sealed record ReactivateIndividualRequest(string Reason);

