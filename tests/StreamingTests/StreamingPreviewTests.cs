using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Streaming;
using Xunit;
using static OoplesFinance.StockIndicators.Tests.Unit.StreamingTests.IndicatorRunner;

namespace OoplesFinance.StockIndicators.Tests.Unit.StreamingTests;

/// <summary>
/// A preview of a bar is what the bar will publish once it is final, and previewing changes nothing.
/// </summary>
/// <remarks>
/// A forming bar is sent with <c>isFinal: false</c>, and every state promises to answer it without
/// committing it. Two ways to break that promise: answering from a mix of this bar and the last - the
/// Ehlers periodogram once divided the previous bar's powers by this bar's maximum - or letting the
/// preview leak into state, so later bars differ from a run that never previewed. Each bar here is
/// previewed with its final values and then committed, so the preview must equal the commit exactly as
/// published, and the committed series must equal a run with no previews at all.
/// </remarks>
public sealed class StreamingPreviewTests : GlobalTestData
{
    private const int Bars = 251;

    [Fact]
    public void APreviewEqualsTheFinalBarAndLeavesNoTrace()
    {
        var bars = StockTestData.Take(Bars)
            .Select(t => new OhlcvBar("TEST", BarTimeframe.Tick, t.Date, t.Date, t.Open, t.High, t.Low, t.Close,
                t.Volume, isFinal: true))
            .ToList();

        var stateType = typeof(IStreamingIndicatorState);
        var types = stateType.Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic && stateType.IsAssignableFrom(t))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        var previewDiffers = new List<string>();
        var previewLeaks = new List<string>();
        var compared = 0;

        foreach (var type in types)
        {
            var build = StreamingCustomInputTests.FindDefaultConstruction(type);
            if (build is null)
            {
                // Named and failed by the custom-input sweep; nothing to preview without a construction.
                continue;
            }

            var clean = StreamingCustomInputTests.Build(build.Value);
            var previewed = StreamingCustomInputTests.Build(build.Value);
            compared++;

            string? differs = null;
            string? leaks = null;
            for (var i = 0; i < bars.Count && (differs is null || leaks is null); i++)
            {
                var expected = clean.Update(bars[i], isFinal: true, includeOutputs: true);
                var preview = previewed.Update(bars[i], isFinal: false, includeOutputs: true);
                var final = previewed.Update(bars[i], isFinal: true, includeOutputs: true);

                differs ??= FirstDifference(preview, final, i);
                leaks ??= FirstDifference(final, expected, i);
            }

            if (differs is not null) { previewDiffers.Add($"{type.Name} {differs}"); }
            if (leaks is not null) { previewLeaks.Add($"{type.Name} {leaks}"); }
        }

        compared.Should().BeGreaterThan(700, "every public streaming state is previewed");

        using var scope = new AssertionScope();
        previewDiffers.Should().BeEmpty(
            $"a preview of a bar publishes what the bar publishes once final ({previewDiffers.Count}): " +
            string.Join(" | ", previewDiffers));
        previewLeaks.Should().BeEmpty(
            $"a preview leaves the state as it found it ({previewLeaks.Count}): " + string.Join(" | ", previewLeaks));
    }

    private static string? FirstDifference(StreamingIndicatorStateResult actual, StreamingIndicatorStateResult expected,
        int bar)
    {
        if (!IsClose(expected.Value, actual.Value))
        {
            return $"{Primary} at bar {bar}: {actual.Value} vs {expected.Value}";
        }

        if (expected.Outputs is null || actual.Outputs is null)
        {
            return expected.Outputs is null == actual.Outputs is null ? null : $"outputs missing at bar {bar}";
        }

        if (!new HashSet<string>(expected.Outputs.Keys, StringComparer.Ordinal).SetEquals(actual.Outputs.Keys))
        {
            return $"output keys differ at bar {bar}: [{string.Join(",", actual.Outputs.Keys)}] vs [{string.Join(",", expected.Outputs.Keys)}]";
        }

        foreach (var kv in expected.Outputs)
        {
            if (!actual.Outputs.TryGetValue(kv.Key, out var value))
            {
                return $"{kv.Key} missing at bar {bar}";
            }

            if (!IsClose(kv.Value, value))
            {
                return $"{kv.Key} at bar {bar}: {value} vs {kv.Value}";
            }
        }

        return null;
    }
}
