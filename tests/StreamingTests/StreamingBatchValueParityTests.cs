using System.Reflection;
using FluentAssertions.Execution;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.StreamingTests;

/// <summary>
/// Every streaming state computes, on every bar, every value its batch twin computes.
/// </summary>
/// <remarks>
/// <para>
/// The hand-written parity specs cover a subset of the catalogue and, for band indicators, usually only the
/// middle band. Forty-nine states disagreed with their batch twins while every spec passed: Bollinger
/// Bands 55% too wide in batch, ATR channels inflated by a first-bar true range of the whole high, a
/// variable moving average that decayed towards zero. This checks all of them, all outputs, every bar.
/// </para>
/// <para>
/// Pairing is by the state's own <see cref="IStreamingIndicatorState.Name"/> through the catalogue's
/// indicator lookup, not by guessing a method name, so a state whose batch method is spelled differently is
/// compared rather than skipped. A state with no batch twin, one that cannot complete a run, or one whose
/// outputs are named differently from its twin's is a named failure. There is no list of exceptions.
/// </para>
/// </remarks>
public sealed class StreamingBatchValueParityTests : GlobalTestData
{
    private const int Bars = 251;
    private const string Primary = "<primary>";

    [Fact]
    public void EveryStreamingStateComputesTheValuesItsBatchTwinComputes()
    {
        var tickers = StockTestData.Take(Bars).ToList();
        var stateType = typeof(IStreamingIndicatorState);
        var types = stateType.Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic && stateType.IsAssignableFrom(t))
            // The custom-input wrapper, not an indicator: it has no defaults and no batch twin.
            .Where(t => t.Name != "CustomInputState")
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        var unpaired = new List<string>();
        var couldNotRun = new List<string>();
        var keyMismatches = new List<string>();
        var disagreements = new List<string>();
        var compared = 0;

        foreach (var type in types)
        {
            var ctor = type.GetConstructors()
                .Where(c => c.GetParameters().All(p => p.HasDefaultValue))
                .OrderBy(c => c.GetParameters().Length)
                .FirstOrDefault();
            if (ctor is null)
            {
                unpaired.Add($"{type.Name} (no constructor without arguments)");
                continue;
            }

            Dictionary<string, List<double>> batch;
            Dictionary<string, List<double>> streamed;
            try
            {
                var state = (IStreamingIndicatorState)ctor.Invoke(ctor.GetParameters().Select(p => p.DefaultValue).ToArray());
                var method = IndicatorInvoker.GetMethod(state.Name);
                if (method is null)
                {
                    unpaired.Add($"{type.Name} (no batch method for {state.Name})");
                    continue;
                }

                batch = RunBatch(method, tickers);
                streamed = RunStreaming(state, tickers);
            }
            catch (Exception ex)
            {
                couldNotRun.Add($"{type.Name} ({(ex.InnerException ?? ex).GetType().Name})");
                continue;
            }

            compared++;

            var batchKeys = new HashSet<string>(batch.Keys, StringComparer.Ordinal);
            var streamKeys = new HashSet<string>(streamed.Keys, StringComparer.Ordinal);
            if (!batchKeys.Contains(Primary))
            {
                // A band indicator publishes its bands and no single series in batch; its streaming value is
                // one of those bands, compared under its own name.
                streamKeys.Remove(Primary);
            }

            if (!batchKeys.SetEquals(streamKeys))
            {
                keyMismatches.Add($"{type.Name}: batch only [{string.Join(",", batchKeys.Except(streamKeys))}], " +
                    $"streaming only [{string.Join(",", streamKeys.Except(batchKeys))}]");
                continue;
            }

            foreach (var key in batchKeys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var expected = batch[key];
                var actual = streamed[key];
                if (expected.Count != actual.Count)
                {
                    disagreements.Add($"{type.Name}.{key}: {expected.Count} batch values, {actual.Count} streamed");
                    continue;
                }

                for (var i = 0; i < expected.Count; i++)
                {
                    if (double.IsNaN(expected[i]) && double.IsNaN(actual[i]))
                    {
                        continue;
                    }

                    var scale = Math.Max(1.0, Math.Max(Math.Abs(expected[i]), Math.Abs(actual[i])));
                    if (!(Math.Abs(expected[i] - actual[i]) <= 1e-9 * scale))
                    {
                        disagreements.Add($"{type.Name}.{key} at bar {i}: batch {expected[i]}, streaming {actual[i]}");
                        break;
                    }
                }
            }
        }

        compared.Should().BeGreaterThan(700, "every public streaming state is compared");

        using var scope = new AssertionScope();
        unpaired.Should().BeEmpty($"every streaming state has a batch twin: {string.Join(" | ", unpaired)}");
        couldNotRun.Should().BeEmpty($"every state completes both runs: {string.Join(" | ", couldNotRun)}");
        keyMismatches.Should().BeEmpty($"both engines name the same outputs: {string.Join(" | ", keyMismatches)}");
        disagreements.Should().BeEmpty($"{compared} states compared; streaming computes what batch computes: " +
            string.Join(" | ", disagreements));
    }

    private static Dictionary<string, List<double>> RunBatch(MethodInfo method, List<TickerData> tickers)
    {
        var parameters = method.GetParameters();
        var args = new object?[parameters.Length];
        args[0] = new StockData(tickers);
        for (var i = 1; i < parameters.Length; i++)
        {
            args[i] = parameters[i].DefaultValue;
        }

        var result = method.Invoke(null, args) as StockData
            ?? throw new InvalidOperationException($"{method.Name} did not return its StockData");

        var series = new Dictionary<string, List<double>>(StringComparer.Ordinal);
        if (result.CustomValuesList.Count > 0)
        {
            series[Primary] = result.CustomValuesList;
        }

        foreach (var output in result.OutputValues)
        {
            series[output.Key] = output.Value;
        }

        return series;
    }

    private static Dictionary<string, List<double>> RunStreaming(IStreamingIndicatorState state, List<TickerData> tickers)
    {
        var series = new Dictionary<string, List<double>>(StringComparer.Ordinal) { [Primary] = new() };
        foreach (var t in tickers)
        {
            var bar = new OhlcvBar("TEST", BarTimeframe.Tick, t.Date, t.Date, t.Open, t.High, t.Low, t.Close, t.Volume, isFinal: true);
            var result = state.Update(bar, isFinal: true, includeOutputs: true);
            series[Primary].Add(result.Value);
            if (result.Outputs is null)
            {
                continue;
            }

            foreach (var output in result.Outputs)
            {
                if (!series.TryGetValue(output.Key, out var list))
                {
                    series[output.Key] = list = new List<double>();
                }

                list.Add(output.Value);
            }
        }

        return series;
    }
}
