using System;

namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>
/// Names the output a streaming state's <see cref="StreamingIndicatorStateResult.Value"/> is.
/// </summary>
/// <remarks>
/// <para>
/// A state publishes a set of named outputs and, separately, one value. Until this was declared, which
/// output that value is was implicit - a reader had to open <c>Update</c> to find out, and nothing stopped a
/// state returning a value that none of its outputs contain. Every state now says it, and
/// <c>PrimaryOutputTests</c> checks, on every bar of the fixture, that the value IS that output.
/// </para>
/// <para>
/// The key is a string because output keys are: they are the keys of <see cref="StreamingIndicatorStateResult.Outputs"/>,
/// named per indicator. The test is what makes a wrong key impossible to ship; the analyzer (SI0005) is
/// what makes a missing one impossible to compile.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class PrimaryOutputAttribute : Attribute
{
    /// <summary>Declares the output key the state's value is.</summary>
    /// <param name="outputKey">A key the state publishes in its outputs.</param>
    /// <exception cref="ArgumentException"><paramref name="outputKey"/> is null or blank.</exception>
    public PrimaryOutputAttribute(string outputKey)
    {
        if (string.IsNullOrWhiteSpace(outputKey))
        {
            throw new ArgumentException("A primary output key is required.", nameof(outputKey));
        }

        OutputKey = outputKey;
    }

    /// <summary>The output key the state's value is.</summary>
    public string OutputKey { get; }
}
