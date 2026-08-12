using System.Reflection;

namespace CebizPay.Domain;

/// <summary>
/// Reference class to identify the Domain assembly in reflection and tests.
/// </summary>
public static class AssemblyReference
{
    /// <summary>
    /// The Domain assembly reference.
    /// </summary>
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
