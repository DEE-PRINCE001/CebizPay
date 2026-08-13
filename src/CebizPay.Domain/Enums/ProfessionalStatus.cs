namespace CebizPay.Domain.Enums;

/// <summary>
/// Represents the work professional status of an individual.
/// Derived from active organization relationships.
/// </summary>
public enum ProfessionalStatus
{
    /// <summary>Not currently affiliated with any active organization.</summary>
    NotAStaff = 1,
    /// <summary>Affiliated as staff with at least one active organization.</summary>
    Staff = 2
}
