using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using TALib;
using Trady.Analysis.Extension;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ExponentialAverageComparison
{
    internal static readonly ComparisonPair[] Pairs =
    [
        new(
            "Skender.GetEma",
            "Ema",
            (data, period) =>
                Aligned(
                    data.Quotes.GetEma(period).Select(row => row.Ema ?? double.NaN).ToArray(),
                    period
                ),
            Reference: Reference
        ),
        new("TaLib.Functions.Ema", "Ema", TaLib, Reference: Reference),
        new(
            "Trady.Indicator.ExponentialMovingAverage",
            "FirstValueEma",
            (data, period) =>
                new(
                    0,
                    data.Candles.Ema(period)
                        .Select(row => (double?)row.Tick ?? double.NaN)
                        .ToArray()
                ),
            FirstValueOoples,
            (data, period) => EvaluateReference(data, period, true)
        ),
        new("QuanTAlib.Ema", "Ema", Quan, Reference: Reference),
    ];

    private static ComparisonSeries Aligned(double[] values, int period) =>
        new(Math.Min(period - 1, values.Length), values);

    private static ComparisonSeries Quan(CompetitorData data, int period)
    {
        // The pinned package's explicit SMA seed matches Skender and TA-Lib.
        var indicator = new QuanTAlib.Ema(period, true);
        return Aligned(
            data.Closes.Select(value =>
                    indicator.Calc(new QuanTAlib.TValue(value, true, false)).Value
                )
                .ToArray(),
            period
        );
    }

    private static ComparisonSeries TaLib(CompetitorData data, int period)
    {
        var packed = new double[data.Count];
        var code = Functions.Ema<double>(
            data.Closes,
            System.Range.All,
            packed,
            out var range,
            period
        );
        if (data.Count == 1 && code == TALib.Core.RetCode.OutOfRangeParam)
            return new(1, [double.NaN]);
        if (code != TALib.Core.RetCode.Success)
            throw new InvalidOperationException("TA-Lib returned " + code);
        var first = Math.Min(period - 1, data.Count);
        var (start, count) = range.GetOffsetAndLength(data.Count);
        if (count != data.Count - first || count > 0 && start != first)
            throw new InvalidOperationException("Unexpected EMA alignment.");
        var values = Enumerable.Repeat(double.NaN, data.Count).ToArray();
        Array.Copy(packed, 0, values, start, count);
        return new(first, values);
    }

    private static ComparisonSeries FirstValueOoples(CompetitorData data, int period)
    {
        var indicator = new FirstValueEma(period);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(0, run[indicator.Outputs[0]].ToArray());
    }

    private static ComparisonSeries Reference(CompetitorData data, int period) =>
        EvaluateReference(data, period, false);

    private static ComparisonSeries EvaluateReference(
        CompetitorData data,
        int period,
        bool firstValue
    )
    {
        var values = Enumerable.Repeat(double.NaN, data.Count).ToArray();
        var first = firstValue ? 0 : Math.Min(period - 1, data.Count);
        if (!firstValue && data.Count < period)
            return new(first, values);
        var seedLength = firstValue ? 1 : period;
        decimal seed = 0;
        for (var i = 0; i < seedLength; i++)
            seed += (decimal)data.Closes[i];
        var average = seed / seedLength;
        values[seedLength - 1] = (double)average;
        for (var i = seedLength; i < data.Count; i++)
        {
            average = (2 * (decimal)data.Closes[i] + (period - 1m) * average) / (period + 1m);
            values[i] = (double)average;
        }
        return new(first, values);
    }
}
