using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Setup-only checks: keep these delegates out of the measured native/builder paths.
internal static class PackedWindowBenchmarkChecks
{
    internal static void Verify(int period, double[] expected, double[] parallel,
        Func<IIndicator> create, Func<IIndicator, IndicatorHistoryMode, IIndicatorRun> build)
    {
        Check(expected, parallel);
        foreach (var mode in new[] { IndicatorHistoryMode.Full, IndicatorHistoryMode.LatestOnly })
        {
            var indicator = create();
            using var result = build(indicator, mode);
            Check(expected, result[indicator].Slice(period - 1).ToArray());
        }
    }

    private static void Check(double[] expected, double[] actual)
    {
        if (expected.Length != actual.Length) throw new InvalidOperationException("Output length mismatch.");
        for (int i = 0; i < expected.Length; i++)
            if (!double.IsFinite(actual[i]) || Math.Abs(expected[i] - actual[i]) > 1e-10 * Math.Max(1, Math.Abs(expected[i])))
                throw new InvalidOperationException($"Packed window mismatch at {i}.");
    }
}
