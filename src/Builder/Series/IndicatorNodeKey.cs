//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright © Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using OoplesFinance.StockIndicators.Builder.Specs;

namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Identifies an indicator node by what it computes rather than by when it was created, so two
/// requests for the same computation on the same input can share one node.
/// </summary>
/// <remarks>
/// <para>
/// The builder already does this for price series - <c>GetOrCreateBaseSeries</c> hands back the same
/// handle when asked for a symbol and timeframe it has already seen. Indicator nodes did not, so
/// asking for an SMA(20) on the close twice built two nodes and computed it twice.
/// </para>
/// <para>
/// A key is only produced when every part of the specification can be compared by value. Options
/// carrying something that cannot - a delegate, or a type with no meaningful value equality - yield
/// no key at all, and the caller then builds a separate node as before. Sharing a node that is not
/// genuinely identical would give a wrong answer; computing one twice only costs time, so the
/// uncertain case fails towards the slower, safe behaviour.
/// </para>
/// </remarks>
internal sealed class IndicatorNodeKey : IEquatable<IndicatorNodeKey>
{
    private static readonly Dictionary<Type, PropertyInfo[]?> PropertyCache = new();
    private static readonly object CacheLock = new();

    private readonly string _key;
    private readonly int _hash;

    private IndicatorNodeKey(string key)
    {
        _key = key;
        _hash = key.GetHashCode();
    }

    /// <summary>
    /// Builds a key for an indicator node, or returns null when the specification cannot be compared
    /// by value.
    /// </summary>
    /// <param name="seriesKey">The symbol and timeframe the node computes on.</param>
    /// <param name="input">The series feeding the indicator.</param>
    /// <param name="spec">The indicator specification.</param>
    public static IndicatorNodeKey? TryCreate(SeriesKey seriesKey, SeriesHandle input, IndicatorSpec spec)
    {
        if (spec is null || spec.Options is null)
        {
            return null;
        }

        var builder = new StringBuilder();
        AppendText(builder, seriesKey.Symbol.ToString());
        AppendText(builder, seriesKey.Timeframe?.ToString());
        builder.Append(input.Id).Append('|')
            .Append((int)spec.Name).Append('|')
            .Append((int)spec.Output).Append('|');
        AppendText(builder, spec.Options.GetType().FullName);

        return TryAppendValue(builder, spec.Options, 0) ? new IndicatorNodeKey(builder.ToString()) : null;
    }

    /// <summary>
    /// Appends free-form text length-first, so it cannot forge a field boundary.
    /// </summary>
    /// <remarks>
    /// A cache key assembled by concatenating text around separators is only unambiguous while the
    /// text cannot contain a separator. A symbol is an arbitrary caller-supplied string, and nothing
    /// stops one containing '|': symbol "A" with timeframe "B|C" and symbol "A|B" with timeframe "C"
    /// would otherwise produce the same key. A colliding key here means two different computations
    /// silently share one node and one of them returns the other's numbers - the failure mode this
    /// type is most obliged to rule out - so the length prefix is worth the four characters even
    /// though no timeframe in the library renders with a '|' today.
    /// </remarks>
    private static void AppendText(StringBuilder builder, string? text)
    {
        var value = text ?? string.Empty;
        builder.Append(value.Length).Append(':').Append(value).Append('|');
    }

    private static bool TryAppendValue(StringBuilder builder, object? value, int depth)
    {
        // Options nest at most a level or two in practice; the limit is here so a cycle cannot spin.
        if (depth > 4)
        {
            return false;
        }

        switch (value)
        {
            case null:
                builder.Append("~null~;");
                return true;
            case string text:
                // Length-first, for the same reason as AppendText: a quoted string parameter
                // containing the closing delimiter would otherwise be indistinguishable from two
                // parameters. No indicator takes a string parameter today; the invariant should not
                // depend on that staying true.
                AppendText(builder, text);
                builder.Append(';');
                return true;
            case bool flag:
                builder.Append(flag ? "true;" : "false;");
                return true;
            case Enum enumValue:
                builder.Append(enumValue.GetType().Name).Append('.').Append(enumValue).Append(';');
                return true;
            case IFormattable formattable when value.GetType().IsPrimitive || value is decimal:
                builder.Append(formattable.ToString("R", CultureInfo.InvariantCulture)).Append(';');
                return true;
            case IEnumerable sequence:
                builder.Append('[');
                foreach (var item in sequence)
                {
                    if (!TryAppendValue(builder, item, depth + 1))
                    {
                        return false;
                    }
                }

                builder.Append("];");
                return true;
        }

        var type = value.GetType();

        // Anything else is only comparable if it is one of our own option objects, which are plain
        // read-only property bags. A delegate, or a type from elsewhere, is not something this can
        // reason about.
        if (value is not IIndicatorSpecOptions)
        {
            return false;
        }

        var properties = GetComparableProperties(type);
        if (properties is null)
        {
            return false;
        }

        builder.Append('{').Append(type.Name).Append(':');
        foreach (var property in properties)
        {
            builder.Append(property.Name).Append('=');

            object? propertyValue;
            try
            {
                propertyValue = property.GetValue(value);
            }
            catch (TargetInvocationException)
            {
                // A computed property that throws tells us nothing about identity, so give up on the
                // whole key rather than pretending the rest of it is sufficient.
                return false;
            }

            if (!TryAppendValue(builder, propertyValue, depth + 1))
            {
                return false;
            }
        }

        builder.Append("};");

        return true;
    }

    private static PropertyInfo[]? GetComparableProperties(Type type)
    {
        lock (CacheLock)
        {
            if (PropertyCache.TryGetValue(type, out var cached))
            {
                return cached;
            }

            var properties = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .ToArray();

            // No readable state means nothing distinguishes two instances, which is more likely a type
            // this does not understand than an indicator with no parameters.
            var result = properties.Length > 0 ? properties : null;
            PropertyCache[type] = result;

            return result;
        }
    }

    /// <inheritdoc/>
    public bool Equals(IndicatorNodeKey? other) =>
        other is not null && string.Equals(_key, other._key, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as IndicatorNodeKey);

    /// <inheritdoc/>
    public override int GetHashCode() => _hash;

    /// <inheritdoc/>
    public override string ToString() => _key;
}
