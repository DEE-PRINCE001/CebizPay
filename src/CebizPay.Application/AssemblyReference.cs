using System.Reflection;

namespace CebizPay.Application;

/// <summary>
/// Reference class to identify the Application assembly in reflection and tests.
/// </summary>
public static class AssemblyReference
{
    /// <summary>
    /// The Application assembly reference.
    /// </summary>
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
