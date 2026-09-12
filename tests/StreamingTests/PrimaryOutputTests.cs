using System.Reflection;
using FluentAssertions.Execution;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.StreamingTests;

/// <summary>
/// Every streaming state declares which of its outputs its value is, and the declaration is true.
/// </summary>
/// <remarks>
/// Checked on every bar of the fixture, not sampled: a value that matches its declared output on most bars
/// and something else on a few is exactly the kind of drift this exists to catch.
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
        var checkedCount = 0;

        foreach (var type in types)
        {
            var declared = type.GetCustomAttribute<PrimaryOutputAttribute>();
            if (declared is null)
            {
                undeclared.Add(type.Name);
                continue;
            }

            var ctor = type.GetConstructors()
                .Where(c => c.GetParameters().All(p => p.HasDefaultValue))
                .OrderBy(c => c.GetParameters().Length)
                .First();
            var state = (IStreamingIndicatorState)ctor.Invoke(ctor.GetParameters().Select(p => p.DefaultValue).ToArray());

            for (var i = 0; i < bars.Count; i++)
            {
                var result = state.Update(bars[i], isFinal: true, includeOutputs: true);
                if (result.Outputs is null || !result.Outputs.TryGetValue(declared.OutputKey, out var output))
                {
                    unpublished.Add($"{type.Name} declares '{declared.OutputKey}', not published at bar {i}");
                    break;
                }

                var same = (double.IsNaN(output) && double.IsNaN(result.Value)) || output == result.Value;
                if (!same)
                {
                    mismatched.Add($"{type.Name}: value {result.Value} but '{declared.OutputKey}' {output} at bar {i}");
                    break;
                }
            }

            checkedCount++;
        }

        checkedCount.Should().BeGreaterThan(700, "every public streaming state is checked");

        using var scope = new AssertionScope();
        undeclared.Should().BeEmpty($"every state must declare its primary output: {string.Join(", ", undeclared)}");
        unpublished.Should().BeEmpty($"a declared primary must be one of the state's outputs: {string.Join(" | ", unpublished)}");
        mismatched.Should().BeEmpty($"a state's value must be its declared primary output: {string.Join(" | ", mismatched)}");
    }
}
