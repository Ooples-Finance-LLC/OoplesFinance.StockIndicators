using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class QuanAtrComparison
{
    internal static readonly ComparisonPair Pair = new(
        "QuanTAlib.Atr",
        "ScaledTrueRange",
        Competitor,
        Ooples,
        (d, p) => Reference(d, p, false),
        CompetitorReference: (d, p) => Reference(d, p, true)
    );

    private static ComparisonSeries Competitor(CompetitorData data, int period)
    {
        var indicator = new QuanTAlib.Atr(period);
        return new(0, data.Bars.Select(b => indicator.Calc(b).Value).ToArray());
    }

    private static ComparisonSeries Ooples(CompetitorData data, int period)
    {
        var indicator = new ScaledTrueRange(period);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(0, run[indicator.Outputs[0]].ToArray());
    }

    private static ComparisonSeries Reference(CompetitorData data, int period, bool native)
    {
        var values = new double[data.Count];
        for (var i = 0; i < data.Count; i++)
        {
            if (native)
            {
                var range = Subtract(data.Highs[i], data.Lows[i]);
                if (i > 0)
                    range = Math.Max(
                        range,
                        Math.Max(
                            Math.Abs(Subtract(data.Highs[i], data.Closes[i - 1])),
                            Math.Abs(Subtract(data.Lows[i], data.Closes[i - 1]))
                        )
                    );
                values[i] = Multiply(Divide(1, period), range);
            }
            else
            {
                var high = Units(data.Highs[i]);
                var low = Units(data.Lows[i]);
                var range = high - low;
                if (i > 0)
                {
                    var previous = Units(data.Closes[i - 1]);
                    range = BigInteger.Max(
                        range,
                        BigInteger.Max(
                            BigInteger.Abs(high - previous),
                            BigInteger.Abs(low - previous)
                        )
                    );
                }
                values[i] = Round(range, Grid * period);
            }
        }
        return new(0, values);
    }
}
