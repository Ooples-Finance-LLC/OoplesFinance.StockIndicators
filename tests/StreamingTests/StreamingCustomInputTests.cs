using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using FluentAssertions.Execution;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Streaming;
using Xunit;
using static OoplesFinance.StockIndicators.Tests.Unit.StreamingTests.IndicatorRunner;

namespace OoplesFinance.StockIndicators.Tests.Unit.StreamingTests;

/// <summary>
/// Every indicator takes a caller-supplied input series, and streaming computes on it exactly what the batch
/// computes.
/// </summary>
/// <remarks>
/// <para>
/// Both engines let a caller compute an indicator on something other than the close. In batch that
/// is chaining - <c>data.CalculateMedianPrice().CalculateRsi()</c> - and a chained series always wins.
/// In streaming it is <see cref="CustomInputState"/>, which wraps any state with a
/// <c>Func&lt;OhlcvBar, double&gt;</c>. Callers pass values, not a name for them, so nothing here goes
/// through InputName.
/// </para>
/// <para>
/// <b>Two rules.</b> First, every state must be reachable by custom input at all: a state that cannot be
/// built for the wrapper, or that has no batch twin, is a named failure rather than a skip. Second, on the
/// close and on a custom series alike, streaming must publish the same outputs as its batch twin, with the
/// same values on every bar, within <see cref="IndicatorRunner.IsClose"/>. An earlier version only checked
/// that both engines moved when the input changed, which a pair computing different numbers passed, and
/// which needed an escape for indicators whose window never filled. Comparing the values needs neither.
/// </para>
/// <para>
/// <b>Two traps in the harness itself.</b> The state is built once with the constructor whose parameters
/// all have defaults, and both runs use that construction and differ only in the selector. And
/// <c>Result.Value</c> is only one member of a state's output set; comparing it alone misses a state whose
/// input moves only a secondary series, which is how DrunkardWalk escaped an earlier sweep. Every key is
/// compared, on both sides.
/// </para>
/// </remarks>
public sealed class StreamingCustomInputTests : GlobalTestData
{
    /// <summary>The whole fixture, so long-window indicators get as much warmup as exists.</summary>
    private const int Bars = 251;

    /// <summary>A custom series the test feeds both engines.</summary>
    public enum CustomSeries
    {
        /// <summary>
        /// Median price: inside every bar's range, so an indicator that reads true highs and lows
        /// legitimately does not move.
        /// </summary>
        InRange,

        /// <summary>
        /// The natural log of the close: outside every bar's range, so high and low come from the
        /// series itself under the per-bar rule, and a non-linear transform, so it is a genuinely
        /// different series even to an indicator that ignores scale.
        /// </summary>
        OutOfRange,
    }

    [Theory]
    [InlineData(CustomSeries.InRange)]
    [InlineData(CustomSeries.OutOfRange)]
    public void StreamingComputesWhatTheBatchComputesOnACustomInput(CustomSeries series)
    {
        var bars = StockTestData.Take(Bars).ToList();
        bars.Should().NotBeEmpty();

        var stateType = typeof(IStreamingIndicatorState);
        var types = stateType.Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic && stateType.IsAssignableFrom(t))
            // The wrapper under test, not an indicator: it has no defaults to build it with and no
            // batch twin, and it is what every other type here is driven through.
            .Where(t => t != typeof(CustomInputState))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        var cannotTakeInput = new List<string>();
        var unpaired = new List<string>();
        var couldNotRun = new List<string>();
        var disagreements = new List<string>();
        var compared = 0;

        foreach (var type in types)
        {
            var build = FindDefaultConstruction(type);
            if (build is null)
            {
                cannotTakeInput.Add(type.Name);
                continue;
            }

            Dictionary<string, List<double>> batchOnClose;
            Dictionary<string, List<double>> batchOnCustom;
            Dictionary<string, List<double>> streamOnClose;
            Dictionary<string, List<double>> streamOnCustom;
            try
            {
                var state = Build(build.Value);
                var batch = IndicatorInvoker.GetMethod(state.Name);
                if (batch is null)
                {
                    unpaired.Add($"{type.Name} (no batch method for {state.Name})");
                    continue;
                }

                batchOnClose = Flatten((StockData)InvokeBatch(batch, bars, custom: null));
                batchOnCustom = Flatten((StockData)InvokeBatch(batch, bars, series));
                streamOnClose = Run(new CustomInputState(state, Close), bars);
                streamOnCustom = Run(new CustomInputState(Build(build.Value), Selector(series)), bars);
            }
            catch (Exception ex)
            {
                // Named, not swallowed: a state that throws on a plain run is worth knowing about.
                couldNotRun.Add($"{type.Name} ({(ex.InnerException ?? ex).GetType().Name})");
                continue;
            }

            compared++;
            Compare($"{type.Name} on the close", batchOnClose, streamOnClose, disagreements);
            Compare($"{type.Name} on the {series} series", batchOnCustom, streamOnCustom, disagreements);
        }

        compared.Should().BeGreaterThan(700, "every public streaming state is compared");

        // All lists in one run: they are independent defects, and a first assertion that throws would hide
        // the others behind it.
        using var scope = new AssertionScope();

        cannotTakeInput.Should().BeEmpty(
            $"every indicator must take custom values through CustomInputState, which needs a way to " +
            $"build the state with its defaults; {cannotTakeInput.Count} cannot be built: " +
            $"{string.Join(", ", cannotTakeInput)}");

        unpaired.Should().BeEmpty($"every streaming state has a batch twin: {string.Join(" | ", unpaired)}");

        couldNotRun.Should().BeEmpty(
            $"every indicator must complete both the batch and the streaming run: {string.Join(", ", couldNotRun)}");

        disagreements.Should().BeEmpty(
            $"streaming computes what batch computes on the close and on the {series} series; {compared} " +
            $"indicators compared. Disagreements ({disagreements.Count}): {string.Join(" | ", disagreements)}");
    }

    private static void Compare(string label, Dictionary<string, List<double>> batch,
        Dictionary<string, List<double>> streamed, List<string> disagreements)
    {
        var batchKeys = new HashSet<string>(batch.Keys, StringComparer.Ordinal);
        var streamKeys = new HashSet<string>(streamed.Keys, StringComparer.Ordinal);
        if (!batchKeys.Contains(Primary))
        {
            // A band indicator publishes its bands and no single series in batch; its streaming value is one
            // of those bands, compared under its own name.
            streamKeys.Remove(Primary);
        }

        if (!batchKeys.SetEquals(streamKeys))
        {
            disagreements.Add($"{label}: batch only [{string.Join(",", batchKeys.Except(streamKeys))}], " +
                $"streaming only [{string.Join(",", streamKeys.Except(batchKeys))}]");
            return;
        }

        foreach (var key in batchKeys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var expected = batch[key];
            var actual = streamed[key];
            if (expected.Count != actual.Count)
            {
                disagreements.Add($"{label} {key}: {expected.Count} batch values, {actual.Count} streamed");
                continue;
            }

            for (var i = 0; i < expected.Count; i++)
            {
                if (!IsClose(expected[i], actual[i]))
                {
                    disagreements.Add($"{label} {key} at bar {i}: batch {expected[i]}, streaming {actual[i]}");
                    break;
                }
            }
        }
    }

    private static double Close(OhlcvBar bar) => bar.Close;

    private static double Median(OhlcvBar bar) => (bar.High + bar.Low) / 2;

    private static double LogClose(OhlcvBar bar) => Math.Log(bar.Close);

    private static Func<OhlcvBar, double> Selector(CustomSeries series) =>
        series == CustomSeries.InRange ? Median : LogClose;

    /// <summary>
    /// Runs a batch indicator on a close series or a custom series chained in front of it.
    /// </summary>
    /// <remarks>
    /// Chaining is the one lever: a chained series always wins. BOTH runs chain a series, mirroring the
    /// streaming side's selectors; an unchained baseline is not "close" for every method, since some read a
    /// median price by default.
    /// </remarks>
    private static object InvokeBatch(MethodInfo batch, List<TickerData> bars, CustomSeries? custom)
    {
        var ps = batch.GetParameters();
        var args = new object?[ps.Length];
        for (var i = 1; i < ps.Length; i++) { args[i] = ps[i].DefaultValue; }

        var data = new StockData(bars);
        var values = custom switch
        {
            CustomSeries.InRange => data.HighPrices.Zip(data.LowPrices, (h, l) => (h + l) / 2).ToList(),
            CustomSeries.OutOfRange => data.ClosePrices.Select(c => Math.Log(c)).ToList(),
            _ => new List<double>(data.ClosePrices),
        };
        data.SetCustomValues(values);

        args[0] = data;

        return batch.Invoke(null, args)
            ?? throw new InvalidOperationException($"{batch.Name} returned null instead of its StockData");
    }

    internal static IStreamingIndicatorState Build((ConstructorInfo Ctor, object?[] Args) build) =>
        (IStreamingIndicatorState)build.Ctor.Invoke(build.Args);

    /// <summary>
    /// How to build the state as a caller would with no arguments: a constructor whose parameters all
    /// have defaults, preferring the one with the fewest parameters.
    /// </summary>
    /// <returns>Null when no constructor can be called without arguments.</returns>
    internal static (ConstructorInfo Ctor, object?[] Args)? FindDefaultConstruction(Type type)
    {
        var ctor = type.GetConstructors()
            .Where(c => c.GetParameters().All(p => p.HasDefaultValue))
            .OrderBy(c => c.GetParameters().Length)
            .FirstOrDefault();

        return ctor is null
            ? null
            : (ctor, ctor.GetParameters().Select(p => p.DefaultValue).ToArray());
    }

    /// <summary>Every published series of a batch result, keyed by output name.</summary>
    private static Dictionary<string, List<double>> Flatten(StockData result)
    {
        var all = new Dictionary<string, List<double>>(StringComparer.Ordinal);
        if (result.CustomValuesList.Count > 0)
        {
            all[Primary] = result.CustomValuesList;
        }

        foreach (var kv in result.OutputValues) { all[kv.Key] = kv.Value; }

        return all;
    }

    /// <summary>Every published series of a streaming run, keyed the same way.</summary>
    private static Dictionary<string, List<double>> Run(IStreamingIndicatorState state, List<TickerData> bars)
    {
        var all = new Dictionary<string, List<double>>(StringComparer.Ordinal) { [Primary] = new() };

        foreach (var t in bars)
        {
            var bar = new OhlcvBar("TEST", BarTimeframe.Tick, t.Date, t.Date,
                t.Open, t.High, t.Low, t.Close, t.Volume, isFinal: true);
            var r = state.Update(bar, isFinal: true, includeOutputs: true);

            all[Primary].Add(r.Value);
            if (r.Outputs is null) { continue; }

            foreach (var kv in r.Outputs)
            {
                if (!all.TryGetValue(kv.Key, out var series)) { all[kv.Key] = series = new List<double>(); }
                series.Add(kv.Value);
            }
        }

        return all;
    }
}
