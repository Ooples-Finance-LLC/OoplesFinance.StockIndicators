using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class SharedWindowBenchmarkValidation
{
    internal static void Cpu(double[] expected, Func<IIndicator> create,
        Func<IIndicator, IndicatorHistoryMode, bool, IIndicatorRun> build)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        foreach (bool reference in new[] { false, true })
        {
            var indicator = create();
            using var run = build(indicator, history, reference);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator].ToArray());
        }
    }

    internal static void Gpu(IIndicator indicator,
        Func<IIndicator, IndicatorHistoryMode, bool, bool, IIndicatorRun> build)
    {
        using var expected = build(indicator, IndicatorHistoryMode.Full, false, false);
        var values = expected[indicator].ToArray();
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var gpu = build(indicator, history, false, true);
            AsinFeasibilityBenchmarks.RequireSame(values, gpu[indicator].ToArray());
        }
    }
}
