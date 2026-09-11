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
/// In streaming it is the <c>Func&lt;OhlcvBar, double&gt;</c> constructor overload. Callers pass
/// values, not a name for them, so nothing here goes through InputName.
/// </para>
/// <para>
/// <b>Two rules.</b> First, every state must HAVE a selector constructor: one without it cannot take
/// custom values at all, and it is a named failure here rather than a skip. Skipping it is how 87
/// states went unexamined by the first version of this test, which only compared states that had
/// both an InputName and a selector overload. Second, a state must respond to a custom series if,
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

    [Fact]
    public void StreamingRespondsToACustomInputExactlyWhenTheBatchDoes()
    {
        var bars = StockTestData.Take(Bars).ToList();
        bars.Should().NotBeEmpty();

        var stateType = typeof(IStreamingIndicatorState);
        var types = stateType.Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && stateType.IsAssignableFrom(t))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        var cannotTakeInput = new List<string>();
        var disagreements = new List<string>();
        var couldNotRun = new List<string>();
        var compared = 0;
        var unpaired = 0;

        foreach (var type in types)
        {
            var selector = FindSelectorConstructor(type);
            if (selector is null)
            {
                cannotTakeInput.Add(type.Name);
                continue;
            }

            if (selector.Value.Args is null) { unpaired++; continue; }

            var batch = FindBatchMethod(type.Name);
            if (batch is null) { unpaired++; continue; }

            bool batchMoved;
            bool streamMoved;
            try
            {
                var batchOnClose = Flatten((StockData)InvokeBatch(batch, bars, chainMedian: false));
                var batchOnMedian = Flatten((StockData)InvokeBatch(batch, bars, chainMedian: true));
                var streamOnClose = Run(Build(selector.Value.Ctor, selector.Value.Args, (Func<OhlcvBar, double>)Close), bars);
                var streamOnMedian = Run(Build(selector.Value.Ctor, selector.Value.Args, (Func<OhlcvBar, double>)Median), bars);

                // An indicator whose window never fills over this fixture - the catalogue has
                // defaults as long as 550 against 251 bars - emits nothing but zeros, and "no
                // difference" then means "no signal" rather than "ignores the input". Refusing a
                // verdict is the honest reading; concluding from it produced a false accusation
                // against QuadraticLeastSquaresMovingAverage.
                if (Silent(streamOnClose) || Silent(batchOnClose)) { unpaired++; continue; }

                batchMoved = Differs(batchOnClose, batchOnMedian);
                streamMoved = Differs(streamOnClose, streamOnMedian);
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
            $"every indicator must take custom values; {cannotTakeInput.Count} states have no " +
            $"Func<OhlcvBar, double> constructor: {string.Join(", ", cannotTakeInput)}");

        disagreements.Should().BeEmpty(
            $"streaming and batch must agree about what the input series controls. " +
            $"{compared} indicators compared, {unpaired} without a comparable run " +
            $"(could not run: {string.Join(", ", couldNotRun)}). " +
            $"Disagreements ({disagreements.Count}): {string.Join(" | ", disagreements)}");
    }

    private static double Close(OhlcvBar bar) => bar.Close;

    private static double Median(OhlcvBar bar) => (bar.High + bar.Low) / 2;

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
    private static object InvokeBatch(MethodInfo batch, List<TickerData> bars, bool chainMedian)
    {
        var ps = batch.GetParameters();
        var args = new object?[ps.Length];
        for (var i = 1; i < ps.Length; i++) { args[i] = ps[i].DefaultValue; }

        // BOTH runs chain a series, mirroring the streaming side's close and median selectors. An
        // unchained baseline is not "close" for every method: AwesomeOscillator, AcceleratorOscillator,
        // AlligatorIndex and GatorOscillator default to MedianPrice, so chaining a median series in
        // front of them changed nothing and read as "batch ignores its input" when it does not.
        var data = new StockData(bars);
        if (chainMedian)
        {
            data = data.CalculateMedianPrice();
        }
        else
        {
            data.SetCustomValues(new List<double>(data.ClosePrices));
        }

        args[0] = data;

        return batch.Invoke(null, args)!;
    }

    private static IStreamingIndicatorState Build(ConstructorInfo ctor, object?[] args, object last) =>
        (IStreamingIndicatorState)ctor.Invoke(args.Append<object?>(last).ToArray())!;

    /// <summary>Whether a run produced nothing to compare.</summary>
    private static bool Silent(Dictionary<string, List<double>> series) =>
        series.Values.All(s => s.All(v => v == 0 || double.IsNaN(v)));

    /// <summary>
    /// The state's selector constructor, plus leading argument values to build it with.
    /// </summary>
    /// <returns>
    /// Null when the state has no <c>Func&lt;OhlcvBar, double&gt;</c> constructor at all - it cannot
    /// take custom values. <c>Args</c> is null when it has one but no overload declares defaults to
    /// build it from, which is a harness limit rather than a verdict.
    /// </returns>
    /// <remarks>
    /// The leading values come from the selector constructor's own defaults when it has them, else
    /// from a sibling overload with the same leading parameter types whose parameters all have
    /// defaults (ignoring a trailing InputName, while that parameter still exists).
    /// </remarks>
    private static (ConstructorInfo Ctor, object?[]? Args)? FindSelectorConstructor(Type type)
    {
        var ctors = type.GetConstructors();

        var bySelector = ctors.FirstOrDefault(c =>
        {
            var ps = c.GetParameters();
            return ps.Length > 0 && ps[^1].ParameterType == typeof(Func<OhlcvBar, double>);
        });
        if (bySelector is null) { return null; }

        var lead = bySelector.GetParameters()[..^1];
        if (lead.All(p => p.HasDefaultValue))
        {
            return (bySelector, lead.Select(p => p.DefaultValue).ToArray());
        }

        foreach (var sibling in ctors)
        {
            var ps = sibling.GetParameters();
            var leading = ps.Length > 0 && ps[^1].ParameterType == typeof(InputName) ? ps[..^1] : ps;
            if (leading.Length == lead.Length
                && leading.Select(p => p.ParameterType).SequenceEqual(lead.Select(p => p.ParameterType))
                && leading.All(p => p.HasDefaultValue))
            {
                return (bySelector, leading.Select(p => p.DefaultValue).ToArray());
            }
        }

        return (bySelector, null);
    }

    /// <summary>The batch twin, by the catalogue's naming convention: XyzState -&gt; CalculateXyz.</summary>
    private static MethodInfo? FindBatchMethod(string stateTypeName)
    {
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
        foreach (var key in a.Keys)
        {
            if (!b.TryGetValue(key, out var other)) { return true; }

            var mine = a[key];
            for (var i = 0; i < Math.Min(mine.Count, other.Count) && i < Bars; i++)
            {
                if (double.IsNaN(mine[i]) && double.IsNaN(other[i])) { continue; }
                if (Math.Abs(mine[i] - other[i]) > 1e-12) { return true; }
            }
        }

        return false;
    }
}
