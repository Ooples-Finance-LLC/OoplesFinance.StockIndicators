using Skender.Stock.Indicators;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class SharedBenchmarkSupport
{
    // Keep source ownership, indicator registration, history and execution order identical.
    internal static IIndicatorRun BuildCpu(Bar[] bars, IIndicator indicator, IndicatorHistoryMode history) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator)
            .ConfigureHistory(history).ConfigureExecution(IndicatorExecutionBackend.Cpu)
            .BuildAsync().GetAwaiter().GetResult();

    internal static IIndicatorRun BuildChecked(StockIndicatorBuilder builder, bool gpu, string failure)
    {
        var run = builder.BuildAsync().GetAwaiter().GetResult();
        if (gpu && builder.LastExecution?.Backend != IndicatorExecutionBackend.Gpu)
        {
            run.Dispose();
            throw new InvalidOperationException(failure);
        }
        return run;
    }

    internal static void RequireExactNullable(double?[] expected, double?[] actual)
    {
        if (expected.Length != actual.Length) throw new InvalidOperationException("Nullable output length differs.");
        for (int i = 0; i < expected.Length; i++)
            if (expected[i].HasValue != actual[i].HasValue || expected[i] is double value
                && BitConverter.DoubleToInt64Bits(value) != BitConverter.DoubleToInt64Bits(actual[i]!.Value))
                throw new InvalidOperationException($"Nullable value/presence differs at {i}.");
    }

    internal static void RequireNativeTolerance(double[] expected, double[] actual)
    {
        if (expected.Length != actual.Length) throw new InvalidOperationException("Native output length mismatch.");
        for (int i = 0; i < expected.Length; i++)
            if (!double.IsFinite(expected[i]) || !double.IsFinite(actual[i])
                || Math.Abs(expected[i] - actual[i]) > 1e-10 * Math.Max(1, Math.Abs(expected[i])))
                throw new InvalidOperationException($"Native mismatch at {i}.");
    }

    internal static void VerifyMappedOutputs(Bar[] bars, Func<IIndicator> create,
        ComparisonSeries expected, string[] names, bool includeScalarStartup = false)
    {
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var owner = create();
            using var run = BuildCpu(bars, owner, mode);
            bool masks = owner.Outputs.Count == 2 * names.Length;
            for (int slot = 0; slot < names.Length; slot++)
            {
                var reference = expected.Outputs[names[slot]];
                if (masks)
                {
                    var present = reference.Present ?? reference.Values.Select((v, i) => i >= reference.FirstValid && !double.IsNaN(v)).ToArray();
                    AsinFeasibilityBenchmarks.RequireSame(reference.Values.Select((v, i) => present[i] ? v : 0).ToArray(), run[owner.Outputs[slot]].ToArray());
                    AsinFeasibilityBenchmarks.RequireSame(present.Select(p => p ? 1d : 0d).ToArray(), run[owner.Outputs[slot + names.Length]].ToArray());
                }
                else if (includeScalarStartup)
                {
                    // Raw stochastic publishes fifty during startup and flat windows.
                    AsinFeasibilityBenchmarks.RequireSame(reference.Values, run[owner.Outputs[slot]].ToArray());
                }
                else
                {
                    // Native scalar comparisons suppress startup; separate golden tests
                    // retain coverage of the builder's expanding-prefix values.
                    AsinFeasibilityBenchmarks.RequireSame(reference.Values.Skip(reference.FirstValid).ToArray(), run[owner.Outputs[slot]].ToArray().Skip(reference.FirstValid).ToArray());
                }
            }
        }
    }

    internal static void VerifyNullablePair(Bar[] bars, Func<IIndicator> create, double?[][] expected)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = create();
            using var run = BuildCpu(bars, indicator, history);
            for (int slot = 0; slot < 2; slot++)
            {
                AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v ?? 0).ToArray(), run[indicator.Outputs[slot]].ToArray());
                AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v.HasValue ? 1d : 0).ToArray(), run[indicator.Outputs[slot + 2]].ToArray());
            }
        }
    }

    internal static Quote[] ToQuotes(Bar[] bars) => bars.Select(b => new Quote
    {
        Date = b.Time, Open = (decimal)b.Open, High = (decimal)b.High,
        Low = (decimal)b.Low, Close = (decimal)b.Close, Volume = (decimal)b.Volume
    }).ToArray();

    internal static Quote[][] OverlappingQuotes(Quote[] quotes, int count, int period, int chunks) =>
        Enumerable.Range(0, chunks).Select(chunk =>
        {
            int start = (count - period) * chunk / chunks;
            int end = (count - period) * (chunk + 1) / chunks;
            return quotes[start..(end + period)];
        }).ToArray();

    internal static void VerifyNullableColumn(double?[] expected, IIndicatorRun run,
        IIndicatorOutput value, IIndicatorOutput presence, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (BitConverter.DoubleToInt64Bits(expected[i] ?? 0) != BitConverter.DoubleToInt64Bits(run[value][i])
                || run[presence][i] != (expected[i].HasValue ? 1d : 0d)) // NOSONAR: S1244 - Presence columns are discrete 0/1 flags, not approximate measurements.
                throw new InvalidOperationException($"Exact nullable builder mismatch at {i}.");
        }
    }
}
