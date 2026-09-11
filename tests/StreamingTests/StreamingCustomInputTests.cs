using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using FluentAssertions.Execution;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Streaming;
using Xunit;

namespace OoplesFinance.StockIndicators.Tests.Unit.StreamingTests;

/// <summary>
/// Every indicator takes a caller-supplied input series, and streaming responds to one exactly when
/// the batch does.
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
/// <b>Two rules.</b> First, every state must be reachable by custom input at all. The first version of
/// this test only compared states that had both an InputName and a selector overload, and 82 states
/// had no selector overload, so they went unexamined; a state that cannot be built for the wrapper is
/// now a named failure rather than a skip. Second, a state must respond to a custom series if,
/// and only if, its batch twin does. That rule is an equivalence rather than "must respond" because
/// some indicators legitimately read specific bar fields - a median-price series lies inside the
/// bar's range, so an Ichimoku line built on true highs and lows does not move - and an equivalence
/// needs no list of exceptions. A list of exceptions is a trapdoor: any genuine defect can be
/// silenced with a name and a plausible sentence.
/// </para>
/// <para>
/// <b>Two traps in the harness itself.</b> The selector constructor usually declares no defaults, so
/// filling its leading parameters with <c>default</c> builds a state with <c>length: 0</c>. Both runs
/// therefore use the SAME constructor and the SAME leading arguments, taken from a sibling overload's
/// defaults, and differ only in the selector. And <c>Result.Value</c> is only one member of a state's
/// output set; comparing it alone misses a state whose input moves only a secondary series, which is
/// how DrunkardWalk escaped an earlier sweep. Every key is compared, on both sides.
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
        /// <remarks>
        /// The first out-of-range probe divided the close by 100. That is a pure rescale, and a
        /// scale-invariant indicator - momentum as a ratio, a z-score, anything normalised - is right
        /// to give the same answer for it, so the probe could not tell a correct "ignores" from a
        /// defect. What it reported instead was floating-point noise on whichever side happened to
        /// round differently.
        /// </remarks>
        OutOfRange,
    }

    [Theory]
    [InlineData(CustomSeries.InRange)]
    [InlineData(CustomSeries.OutOfRange)]
    public void StreamingRespondsToACustomInputExactlyWhenTheBatchDoes(CustomSeries series)
    {
        var bars = StockTestData.Take(Bars).ToList();
        bars.Should().NotBeEmpty();

        var stateType = typeof(IStreamingIndicatorState);
        var types = stateType.Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && stateType.IsAssignableFrom(t))
            // The wrapper under test, not an indicator: it has no defaults to build it with and no
            // batch twin, and it is what every other type here is driven through.
            .Where(t => t != typeof(CustomInputState))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        var cannotTakeInput = new List<string>();
        var disagreements = new List<string>();
        var couldNotRun = new List<string>();
        var compared = 0;
        var unpaired = 0;

        foreach (var type in types)
        {
            var build = FindDefaultConstruction(type);
            if (build is null)
            {
                cannotTakeInput.Add(type.Name);
                continue;
            }

            var batch = FindBatchMethod(type.Name);
            if (batch is null) { unpaired++; continue; }

            bool batchMoved;
            bool streamMoved;
            try
            {
                var batchOnClose = Flatten((StockData)InvokeBatch(batch, bars, custom: null));
                var batchOnCustom = Flatten((StockData)InvokeBatch(batch, bars, series));
                var streamOnClose = Run(new CustomInputState(Build(build.Value), Close), bars);
                var streamOnCustom = Run(new CustomInputState(Build(build.Value), Selector(series)), bars);

                // An indicator whose window never fills over this fixture - the catalogue has
                // defaults as long as 550 against 251 bars - emits nothing but zeros, and "no
                // difference" then means "no signal" rather than "ignores the input". Refusing a
                // verdict is the honest reading; concluding from it produced a false accusation
                // against QuadraticLeastSquaresMovingAverage.
                if (Silent(streamOnClose) || Silent(batchOnClose)) { unpaired++; continue; }

                batchMoved = Differs(batchOnClose, batchOnCustom);
                streamMoved = Differs(streamOnClose, streamOnCustom);
            }
            catch (Exception ex)
            {
                // Named, not swallowed: a state that throws on a plain run is worth knowing about.
                couldNotRun.Add($"{type.Name} ({(ex.InnerException ?? ex).GetType().Name})");
                unpaired++;
                continue;
            }

            compared++;

            if (batchMoved != streamMoved)
            {
                disagreements.Add(
                    $"{type.Name}: batch {(batchMoved ? "responds" : "ignores")}, " +
                    $"streaming {(streamMoved ? "responds" : "ignores")}");
            }
        }

        compared.Should().BeGreaterThan(150, "the harness must exercise a broad set of indicators");

        // Both lists in one run: they are independent defects, and a first assertion that throws
        // would hide the second list behind it.
        using var scope = new AssertionScope();

        cannotTakeInput.Should().BeEmpty(
            $"every indicator must take custom values through CustomInputState, which needs a way to " +
            $"build the state with its defaults; {cannotTakeInput.Count} cannot be built: " +
            $"{string.Join(", ", cannotTakeInput)}");

        couldNotRun.Should().BeEmpty(
            $"every indicator must complete both the batch and the streaming run: {string.Join(", ", couldNotRun)}");

        disagreements.Should().BeEmpty(
            $"streaming and batch must agree about what the {series} input series controls. " +
            $"{compared} indicators compared, {unpaired} without a comparable run " +
            $"(could not run: {string.Join(", ", couldNotRun)}). " +
            $"Disagreements ({disagreements.Count}): {string.Join(" | ", disagreements)}");
    }

    private static double Close(OhlcvBar bar) => bar.Close;

    private static double Median(OhlcvBar bar) => (bar.High + bar.Low) / 2;

    private static double LogClose(OhlcvBar bar) => Math.Log(bar.Close);

    private static Func<OhlcvBar, double> Selector(CustomSeries series) =>
        series == CustomSeries.InRange ? Median : LogClose;

    /// <summary>
    /// Runs a batch indicator on a close series or a median-price series chained in front of it.
    /// </summary>
    /// <remarks>
    /// Chaining is the one lever: a chained series always wins. Some methods still take their own
    /// <c>inputName</c> parameter and resolve it through the two-argument
    /// <c>GetInputValuesList(inputName, stockData)</c>, which never looks at the chained series -
    /// AwesomeOscillator defaults it to MedianPrice. An earlier version of this test drove those
    /// through the parameter instead, which hid exactly that defect: they ignore the chain.
    /// </remarks>
    private static object InvokeBatch(MethodInfo batch, List<TickerData> bars, CustomSeries? custom)
    {
        var ps = batch.GetParameters();
        var args = new object?[ps.Length];
        for (var i = 1; i < ps.Length; i++) { args[i] = ps[i].DefaultValue; }

        // BOTH runs chain a series, mirroring the streaming side's selectors. An unchained baseline
        // is not "close" for every method: AwesomeOscillator, AcceleratorOscillator, AlligatorIndex
        // and GatorOscillator default to MedianPrice, so chaining a median series in front of them
        // changed nothing and read as "batch ignores its input" when it does not.
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

    private static IStreamingIndicatorState Build((ConstructorInfo Ctor, object?[] Args) build) =>
        (IStreamingIndicatorState)build.Ctor.Invoke(build.Args);

    /// <summary>Whether a run produced nothing to compare.</summary>
    private static bool Silent(Dictionary<string, List<double>> series) =>
        series.Values.All(s => s.All(v => v == 0 || double.IsNaN(v)));

    /// <summary>
    /// How to build the state as a caller would with no arguments: a constructor whose parameters all
    /// have defaults, preferring the one with the fewest parameters.
    /// </summary>
    /// <returns>Null when no constructor can be called without arguments.</returns>
    /// <remarks>
    /// Custom input no longer comes from a per-state selector overload - <see cref="CustomInputState"/>
    /// wraps any state - so what matters is only that the state can be built. Both runs build it the
    /// same way and differ only in the selector, which rules out the old trap of two overloads being
    /// filled with different argument values.
    /// </remarks>
    private static (ConstructorInfo Ctor, object?[] Args)? FindDefaultConstruction(Type type)
    {
        var ctor = type.GetConstructors()
            .Where(c => c.GetParameters().All(p => p.HasDefaultValue))
            .OrderBy(c => c.GetParameters().Length)
            .FirstOrDefault();

        return ctor is null
            ? null
            : (ctor, ctor.GetParameters().Select(p => p.DefaultValue).ToArray());
    }

    /// <summary>The batch twin, by the catalogue's naming convention: XyzState -&gt; CalculateXyz.</summary>
    private static MethodInfo? FindBatchMethod(string stateTypeName)
    {
        if (!stateTypeName.EndsWith("State", StringComparison.Ordinal)) { return null; }

        var name = "Calculate" + stateTypeName[..^"State".Length];

        return typeof(StockData).Assembly.GetTypes()
            .Where(t => t.IsSealed && t.IsAbstract)                       // static classes
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .FirstOrDefault(m =>
                m.Name == name
                && m.ReturnType == typeof(StockData)
                && m.GetParameters().Length > 0
                && m.GetParameters()[0].ParameterType == typeof(StockData)
                && m.GetParameters().Skip(1).All(p => p.HasDefaultValue));
    }



    /// <summary>Every published series of a batch result, keyed by output name.</summary>
    private static Dictionary<string, List<double>> Flatten(StockData result)
    {
        var all = new Dictionary<string, List<double>>(StringComparer.Ordinal)
        {
            ["<primary>"] = result.CustomValuesList,
        };

        foreach (var kv in result.OutputValues) { all[kv.Key] = kv.Value; }

        return all;
    }

    /// <summary>Every published series of a streaming run, keyed the same way.</summary>
    private static Dictionary<string, List<double>> Run(IStreamingIndicatorState state, List<TickerData> bars)
    {
        var all = new Dictionary<string, List<double>>(StringComparer.Ordinal) { ["<primary>"] = new() };

        foreach (var t in bars)
        {
            var bar = new OhlcvBar("TEST", BarTimeframe.Tick, t.Date, t.Date,
                t.Open, t.High, t.Low, t.Close, t.Volume, isFinal: true);
            var r = state.Update(bar, isFinal: true, includeOutputs: true);

            all["<primary>"].Add(r.Value);
            if (r.Outputs is null) { continue; }

            foreach (var kv in r.Outputs)
            {
                if (!all.TryGetValue(kv.Key, out var series)) { all[kv.Key] = series = new List<double>(); }
                series.Add(kv.Value);
            }
        }

        return all;
    }

    private static bool Differs(Dictionary<string, List<double>> a, Dictionary<string, List<double>> b)
    {
        // The union of both runs' keys, and unequal lengths count as a difference: absent data is not
        // agreement. Iterating one side's keys, bounded by the shorter series, read "only the second run
        // published this" or "one run stopped early" as "no change".
        foreach (var key in a.Keys.Union(b.Keys, StringComparer.Ordinal))
        {
            if (!a.TryGetValue(key, out var mine) || !b.TryGetValue(key, out var other)) { return true; }
            if (mine.Count != other.Count) { return true; }

            for (var i = 0; i < mine.Count; i++)
            {
                if (double.IsNaN(mine[i]) && double.IsNaN(other[i])) { continue; }
                // Relative, not absolute. A series in the millions - volume, an accumulation line - carries
                // more than 1e-12 of rounding noise from reordered arithmetic alone, and an absolute
                // threshold read that noise as "responds". A real response to a different input series
                // differs by orders of magnitude more than a part in a billion.
                var scale = Math.Max(1.0, Math.Max(Math.Abs(mine[i]), Math.Abs(other[i])));
                if (Math.Abs(mine[i] - other[i]) > 1e-9 * scale) { return true; }
            }
        }

        return false;
    }
}
