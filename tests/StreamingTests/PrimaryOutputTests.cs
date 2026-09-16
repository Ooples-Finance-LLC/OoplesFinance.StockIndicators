using System.Reflection;
using FluentAssertions.Execution;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.StreamingTests;

/// <summary>
/// Every streaming state declares which of its outputs its value is, and the declaration is true.
/// </summary>
/// <remarks>
/// <para>
/// Checked on every bar of the fixture, not sampled: a value that matches its declared output on most bars
/// and something else on a few is exactly the kind of drift this exists to catch.
/// </para>
/// <para>
/// Checked on preview bars as well as final ones. A streaming caller previews an unclosed bar far more
/// often than it finalises one, and a state that takes a different path for the two - a rolling window
/// that peeks rather than appends, a smoother that does not advance - could hold the invariant on the
/// path this test walks and break it on the path callers use.
/// </para>
/// <para>
/// Checked at a second set of constructor arguments as well as the defaults, because a state whose value
/// and outputs are wired together only at its default length is not a state that has the property.
/// </para>
/// </remarks>
public sealed class PrimaryOutputTests : GlobalTestData
{
    private const int Bars = 251;

    [Fact]
    public void EveryStateDeclaresThePrimaryOutputItsValueIs()
    {
        var bars = StockTestData.Take(Bars)
            .Select(t => new OhlcvBar("TEST", BarTimeframe.Tick, t.Date, t.Date, t.Open, t.High, t.Low, t.Close, t.Volume, isFinal: true))
            .ToList();

        var stateType = typeof(IStreamingIndicatorState);
        var types = stateType.Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic && stateType.IsAssignableFrom(t))
            .Where(t => t != typeof(CustomInputState))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        var undeclared = new List<string>();
        var unpublished = new List<string>();
        var mismatched = new List<string>();
        var uncreatable = new List<string>();
        var checkedCount = 0;

        foreach (var type in types)
        {
            checkedCount++;

            var declared = type.GetCustomAttribute<PrimaryOutputAttribute>();
            if (declared is null)
            {
                undeclared.Add(type.Name);
                continue;
            }

            // SI0001 makes this constructor a build error to omit, so First() cannot throw on a tree that
            // compiles - but say so plainly rather than failing the whole run with a sequence error.
            var ctor = type.GetConstructors()
                .Where(c => c.GetParameters().All(p => p.HasDefaultValue))
                .OrderBy(c => c.GetParameters().Length)
                .FirstOrDefault();
            if (ctor is null)
            {
                uncreatable.Add($"{type.Name} has no constructor callable with no arguments");
                continue;
            }

            var parameters = ctor.GetParameters();
            foreach (var arguments in ArgumentSets(parameters))
            {
                object state;
                try
                {
                    state = ctor.Invoke(arguments.Values);
                }
                catch (TargetInvocationException ex)
                {
                    uncreatable.Add($"{type.Name} at {arguments.Name}: {ex.InnerException?.GetType().Name}");
                    continue;
                }

                Check((IStreamingIndicatorState)state, type, declared.OutputKey, arguments.Name, bars,
                    unpublished, mismatched);
            }
        }

        using var scope = new AssertionScope();
        checkedCount.Should().Be(types.Count, "every public streaming state is examined");
        checkedCount.Should().BeGreaterThan(700, "reflection found the states, rather than an empty set");
        undeclared.Should().BeEmpty($"every state must declare its primary output: {string.Join(", ", undeclared)}");
        uncreatable.Should().BeEmpty($"every state must be constructible to be checked: {string.Join(" | ", uncreatable)}");
        unpublished.Should().BeEmpty($"a declared primary must be one of the state's outputs: {string.Join(" | ", unpublished)}");
        mismatched.Should().BeEmpty($"a state's value must be its declared primary output: {string.Join(" | ", mismatched)}");
    }

    /// <summary>
    /// Walks the fixture, previewing each bar before finalising it, and holds the state to its declaration
    /// on both.
    /// </summary>
    private static void Check(IStreamingIndicatorState state, Type type, string key, string arguments,
        IReadOnlyList<OhlcvBar> bars, List<string> unpublished, List<string> mismatched)
    {
        for (var i = 0; i < bars.Count; i++)
        {
            // The unclosed bar first, then the same bar closed. This is the order a live feed produces.
            if (!Holds(state, bars[i], isFinal: false, type, key, arguments, i, "preview", unpublished, mismatched))
            {
                return;
            }

            if (!Holds(state, bars[i], isFinal: true, type, key, arguments, i, "final", unpublished, mismatched))
            {
                return;
            }
        }
    }

    /// <summary>One update, and whether the state's value was the output it says it is.</summary>
    private static bool Holds(IStreamingIndicatorState state, OhlcvBar bar, bool isFinal, Type type, string key,
        string arguments, int index, string phase, List<string> unpublished, List<string> mismatched)
    {
        var result = state.Update(bar, isFinal, includeOutputs: true);

        if (result.Outputs is null || !result.Outputs.TryGetValue(key, out var output))
        {
            unpublished.Add($"{type.Name} ({arguments}, {phase}) declares '{key}', not published at bar {index}");
            return false;
        }

        var same = (double.IsNaN(output) && double.IsNaN(result.Value)) || output == result.Value;
        if (!same)
        {
            mismatched.Add($"{type.Name} ({arguments}, {phase}): value {result.Value} but '{key}' {output} at bar {index}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// The defaults, and the same constructor at a longer window.
    /// </summary>
    /// <remarks>
    /// Only whole-number arguments move, and only ones with room to move: a length of 1 or 2 is often a
    /// documented degenerate case, and a multiplier or an enum carries meaning that arithmetic on it does
    /// not respect. Shifting the windows is enough to take a state off whatever path its defaults happen
    /// to select.
    /// </remarks>
    private static IEnumerable<(string Name, object?[] Values)> ArgumentSets(ParameterInfo[] parameters)
    {
        var defaults = parameters.Select(p => p.DefaultValue).ToArray();
        yield return ("defaults", defaults);

        var longer = new object?[parameters.Length];
        var moved = false;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].ParameterType == typeof(int) && defaults[i] is int length && length >= 3)
            {
                longer[i] = length + 5;
                moved = true;
            }
            else
            {
                longer[i] = defaults[i];
            }
        }

        if (moved)
        {
            yield return ("longer windows", longer);
        }
    }
}
