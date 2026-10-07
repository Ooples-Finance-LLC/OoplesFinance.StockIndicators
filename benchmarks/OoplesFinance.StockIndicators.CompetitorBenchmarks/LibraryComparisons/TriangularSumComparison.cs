using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class TriangularSumComparison
{
    internal static readonly ComparisonPair[] Pairs =
    [
        new(
            "TaLib.Functions.Trima",
            "TriangularWindowAverage",
            (data, period) => TaLib(data, period, false),
            (data, period) => Ooples(data, period, false),
            (data, period) => Reference(data, period, false)
        ),
        new(
            "QuanTAlib.Trima",
            "TriangularWindowAverage",
            Quan,
            (data, period) => Ooples(data, period, false),
            (data, period) => Reference(data, period, false)
        ),
        new(
            "TaLib.Functions.Sum",
            "RollingPriceSum",
            (data, period) => TaLib(data, period, true),
            (data, period) => Ooples(data, period, true),
            (data, period) => Reference(data, period, true)
        ),
    ];

    private static ComparisonSeries Ooples(CompetitorData data, int period, bool sum)
    {
        IIndicator indicator = sum
            ? new RollingPriceSum(period)
            : new TriangularWindowAverage(period);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(Math.Min(period - 1, data.Count), run[indicator.Outputs[0]].ToArray());
    }

    private static ComparisonSeries Quan(CompetitorData data, int period)
    {
        var indicator = new QuanTAlib.Trima(period);
        return new(
            Math.Min(period - 1, data.Count),
            data.Closes.Select(v => indicator.Calc(new QuanTAlib.TValue(v, true, false)).Value)
                .ToArray()
        );
    }

    private static ComparisonSeries TaLib(CompetitorData data, int period, bool sum)
    {
        var packed = new double[data.Count];
        System.Range range;
        var code = sum
            ? Functions.Sum<double>(data.Closes, System.Range.All, packed, out range, period)
            : Functions.Trima<double>(data.Closes, System.Range.All, packed, out range, period);
        if (data.Count == 1 && code == TALib.Core.RetCode.OutOfRangeParam)
            return new(1, [double.NaN]);
        if (code != TALib.Core.RetCode.Success)
            throw new InvalidOperationException("TA-Lib returned " + code);
        var first = Math.Min(period - 1, data.Count);
        var (start, count) = range.GetOffsetAndLength(data.Count);
        if (count != data.Count - first || count > 0 && start != first)
            throw new InvalidOperationException("Unexpected window alignment.");
        var values = Enumerable.Repeat(double.NaN, data.Count).ToArray();
        Array.Copy(packed, 0, values, start, count);
        return new(first, values);
    }

    private static ComparisonSeries Reference(CompetitorData data, int period, bool sum)
    {
        var values = Enumerable.Repeat(double.NaN, data.Count).ToArray();
        for (var i = period - 1; i < data.Count; i++)
        {
            decimal total = 0,
                weights = 0;
            for (var j = 0; j < period; j++)
            {
                var weight = sum ? 1 : Math.Min(j + 1, period - j);
                total += (decimal)data.Closes[i - period + 1 + j] * weight;
                weights += weight;
            }
            values[i] = (double)(sum ? total : total / weights);
        }
        return new(Math.Min(period - 1, data.Count), values);
    }
}
