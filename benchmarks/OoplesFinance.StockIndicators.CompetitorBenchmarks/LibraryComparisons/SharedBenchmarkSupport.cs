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
}
