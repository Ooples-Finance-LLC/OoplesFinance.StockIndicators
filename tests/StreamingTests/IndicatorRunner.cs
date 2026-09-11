using System.Reflection;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.StreamingTests;

/// <summary>
/// Runs an indicator through either engine and compares values by one rule, for the tests that hold the two
/// engines to each other.
/// </summary>
internal static class IndicatorRunner
{
    /// <summary>The key of an indicator's single series: batch's custom values, streaming's value.</summary>
    public const string Primary = "<primary>";

    /// <summary>
    /// Whether two computed values agree: equal, both NaN, or within 1e-9 of the larger magnitude plus 1e-12.
    /// </summary>
    /// <remarks>
    /// The shape of numpy.testing.assert_allclose and math.isclose: relative to the larger magnitude, plus an
    /// absolute floor for values that should be zero, which a relative test alone can never pass. 1e-9 relative
    /// is math.isclose's default and a hundred times tighter than numpy's. The 1e-12 floor is far below any price
    /// or indicator resolution; a floor of 1 (an absolute 1e-9) passed a batch 0 against a streamed 9e-10 and hid
    /// the warmup and zero-centred defects these tests look for.
    /// </remarks>
    public static bool IsClose(double expected, double actual)
    {
        if (expected == actual || (double.IsNaN(expected) && double.IsNaN(actual)))
        {
            return true;
        }

        var scale = Math.Max(Math.Abs(expected), Math.Abs(actual));
        return Math.Abs(expected - actual) <= (1e-9 * scale) + 1e-12;
    }

    /// <summary>Runs a batch method with its default arguments; its series by output name.</summary>
    public static Dictionary<string, List<double>> RunBatch(MethodInfo method, List<TickerData> tickers)
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

    /// <summary>Streams every bar through a state as a final bar; its series by output name.</summary>
    public static Dictionary<string, List<double>> RunStreaming(IStreamingIndicatorState state, List<TickerData> tickers)
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
