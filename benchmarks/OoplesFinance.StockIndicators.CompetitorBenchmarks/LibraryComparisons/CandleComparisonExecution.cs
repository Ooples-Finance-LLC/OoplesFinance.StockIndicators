using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class CandleComparisonExecution
{
    internal static ComparisonSeries Run(IIndicator indicator, CompetitorData data, int firstValid)
    {
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(firstValid, run[indicator.Outputs[0]].ToArray());
    }

    internal static ComparisonSeries Unpack(
        TALib.Core.RetCode code,
        int[] packed,
        System.Range range,
        int length,
        int firstValid,
        string pattern
    )
    {
        if (length == 1 && code == TALib.Core.RetCode.OutOfRangeParam)
            return new(1, [double.NaN]);
        if (code != TALib.Core.RetCode.Success)
            throw new InvalidOperationException("TA-Lib returned " + code);
        var (start, count) = range.GetOffsetAndLength(length);
        if (count != length - firstValid || count > 0 && start != firstValid)
            throw new InvalidOperationException("Unexpected " + pattern + " alignment.");
        var values = new double[length];
        for (var i = 0; i < count; i++)
            values[start + i] = packed[i];
        return new(firstValid, values);
    }
}
