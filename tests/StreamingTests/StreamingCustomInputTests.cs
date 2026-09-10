using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Streaming;
using Xunit;

namespace OoplesFinance.StockIndicators.Tests.Unit.StreamingTests;

/// <summary>
/// Streaming responds to a caller-supplied input series if, and only if, the batch does.
/// </summary>
/// <remarks>
/// <para>
/// Both engines let a caller compute an indicator on something other than the close. In batch that
/// is chaining - <c>data.CalculateMedianPrice().CalculateRsi()</c>. In streaming it is the
/// <c>Func&lt;OhlcvBar, double&gt;</c> constructor overload. A state can build a
/// <c>StreamingInputResolver</c> and never read it, in which case the selector is accepted and
/// discarded: no exception, no warning, and results that silently disagree with the batch the moment
/// a caller supplies custom values.
/// </para>
/// <para>
/// <b>Why the rule is an equivalence rather than "streaming must respond".</b> Some indicators are
/// defined on specific bar fields - Ichimoku on highs and lows, Fibonacci retracements on the range -
/// and for those there is no series to substitute, so neither engine should move. An
/// "always respond" rule would need a list of exceptions, and a list of exceptions is a trapdoor:
/// any genuine defect can be silenced by adding a name and a plausible sentence. Comparing the two
/// engines instead needs no list. The input-independent indicators pass because their batch does not
/// move either, which is the same evidence a human would have used to justify exempting them.
/// </para>
/// <para>
/// <b>Two traps in the harness itself.</b> The selector constructor usually declares no defaults, so
/// filling its leading parameters with <c>default</c> builds a state with <c>length: 0</c> - the two
/// states then differ because of the length rather than the input, which reads as a pass. Both are
/// therefore constructed from the SAME argument values, taken from the InputName overload's
/// defaults. And <c>Result.Value</c> is only one member of a state's output set, the rest arriving
/// through <c>Outputs</c>; comparing <c>Value</c> alone misses a state whose selected series moves
/// only a secondary series, which is exactly how DrunkardWalk escaped an earlier sweep. Every key is
/// compared, on both sides.
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

        var disagreements = new List<string>();
        var compared = 0;
        var unpaired = 0;

        foreach (var type in types)
        {
            var pair = FindComparablePair(type);
            if (pair is null) { unpaired++; continue; }

            var batch = FindBatchMethod(type.Name);
            if (batch is null) { unpaired++; continue; }

            bool batchMoved;
            bool streamMoved;
            try
            {
                var batchOnClose = Flatten((StockData)InvokeBatch(batch, bars, InputName.Close));
                var batchOnMedian = Flatten((StockData)InvokeBatch(batch, bars, InputName.MedianPrice));
                var streamOnClose = Run(Build(pair.Value.ByName, pair.Value.Args, InputName.Close), bars);
                var streamOnMedian = Run(Build(pair.Value.BySelector, pair.Value.Args, (Func<OhlcvBar, double>)Median), bars);

                // An indicator whose window never fills over this fixture - the catalogue has
                // defaults as long as 550 against 251 bars - emits nothing but zeros, and "no
                // difference" then means "no signal" rather than "ignores the input". Refusing a
                // verdict is the honest reading; concluding from it produced a false accusation
                // against QuadraticLeastSquaresMovingAverage.
                if (Silent(streamOnClose) || Silent(batchOnClose)) { unpaired++; continue; }

                batchMoved = Differs(batchOnClose, batchOnMedian);
                streamMoved = Differs(streamOnClose, streamOnMedian);
            }
            catch
            {
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

        disagreements.Should().BeEmpty(
            $"streaming and batch must agree about what the input series controls. " +
            $"{compared} indicators compared, {unpaired} without a comparable pair. " +
            $"Disagreements: {string.Join(" | ", disagreements)}");
    }

    private static double Median(OhlcvBar bar) => (bar.High + bar.Low) / 2;

    /// <summary>
    /// Runs a batch indicator on a given input series, using whichever lever that indicator exposes.
    /// </summary>
    /// <remarks>
    /// The catalogue has two ways of accepting an input series and they are not interchangeable.
    /// Most indicators read it from the StockData - CustomValuesList if chained, else InputValues -
    /// so chaining is how a caller changes it. A minority declare an <c>InputName</c> parameter and
    /// resolve through the two-argument <c>GetInputValuesList(inputName, stockData)</c>, which never
    /// looks at the chained series at all; AwesomeOscillator is one, and it defaults to
    /// <c>MedianPrice</c> rather than close.
    ///
    /// Testing only the chaining lever reported those as ignoring their input, which they do not -
    /// they ignore that particular lever. Each is driven the way it actually accepts input.
    /// </remarks>
    private static object InvokeBatch(MethodInfo batch, List<TickerData> bars, InputName input)
    {
        var ps = batch.GetParameters();
        var args = new object?[ps.Length];
        for (var i = 1; i < ps.Length; i++) { args[i] = ps[i].DefaultValue; }

        var byParameter = Array.FindIndex(ps, p => p.ParameterType == typeof(InputName));
        if (byParameter > 0)
        {
            args[0] = new StockData(bars);
            args[byParameter] = input;
        }
        else
        {
            args[0] = input == InputName.MedianPrice
                ? new StockData(bars).CalculateMedianPrice()
                : new StockData(bars);
        }

        return batch.Invoke(null, args)!;
    }

    private static IStreamingIndicatorState Build(ConstructorInfo ctor, object?[] args, object last) =>
        (IStreamingIndicatorState)ctor.Invoke(args.Append<object?>(last).ToArray())!;

    /// <summary>Whether a run produced nothing to compare.</summary>
    private static bool Silent(Dictionary<string, List<double>> series) =>
        series.Values.All(s => s.All(v => v == 0 || double.IsNaN(v)));

    /// <summary>
    /// An (InputName, selector) constructor pair differing only in the final parameter, plus the
    /// shared argument values, so the only difference between the two states is the input.
    /// </summary>
    private static (ConstructorInfo ByName, ConstructorInfo BySelector, object?[] Args)? FindComparablePair(Type type)
    {
        var ctors = type.GetConstructors();

        var byName = ctors.FirstOrDefault(c =>
        {
            var ps = c.GetParameters();
            return ps.Length > 0
                && ps[^1].ParameterType == typeof(InputName)
                && ps.Take(ps.Length - 1).All(p => p.HasDefaultValue);
        });
        if (byName is null) { return null; }

        var lead = byName.GetParameters()[..^1];

        var bySelector = ctors.FirstOrDefault(c =>
        {
            var ps = c.GetParameters();
            return ps.Length == lead.Length + 1
                && ps[^1].ParameterType == typeof(Func<OhlcvBar, double>)
                && ps[..^1].Select(p => p.ParameterType).SequenceEqual(lead.Select(p => p.ParameterType));
        });
        if (bySelector is null) { return null; }

        return (byName, bySelector, lead.Select(p => p.DefaultValue).ToArray());
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
