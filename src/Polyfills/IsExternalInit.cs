#if !NET5_0_OR_GREATER

// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Polyfill for init-only setters in C# 9+ on older frameworks.
    /// </summary>
    internal static class IsExternalInit
    {
    }
}

#endif
