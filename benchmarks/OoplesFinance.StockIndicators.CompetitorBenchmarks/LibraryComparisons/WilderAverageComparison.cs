using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class WilderAverageComparison
{
    internal static readonly ComparisonPair[] Pairs = new[]
    {
        "Skender.GetSmma",
        "QuanTAlib.Smma",
        "Trady.Indicator.ModifiedMovingAverage",
    }
        .Select(id => new ComparisonPair(
            id,
            "WilderMovingAverage",
            (data, period) => Competitor(id, data, period),
            (data, period) => Ooples(id, data, period),
            (data, period) => Reference(id, data, period, false),
            CompetitorReference: (data, period) => Reference(id, data, period, true)
        ))
        .ToArray();

    private static bool FirstSeed(string id) => id.StartsWith("Trady.", StringComparison.Ordinal);

    private static int First(string id, int period, int count) =>
        id.StartsWith("Skender.", StringComparison.Ordinal) ? Math.Min(period - 1, count) : 0;

    private static ComparisonSeries Ooples(string id, CompetitorData data, int period)
    {
        var indicator = new WilderMovingAverage(period, !FirstSeed(id));
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(First(id, period, data.Count), run[indicator.Outputs[0]].ToArray());
    }

    private static ComparisonSeries Competitor(string id, CompetitorData data, int period)
    {
        if (FirstSeed(id))
            return new(
                0,
                new Trady.Analysis.Indicator.ModifiedMovingAverage(data.Candles, period)
                    .Compute()
                    .Select(v => (double?)v.Tick ?? double.NaN)
                    .ToArray()
            );
        if (id.StartsWith("Skender.", StringComparison.Ordinal))
            return new(
                First(id, period, data.Count),
                data.Quotes.GetSmma(period).Select(v => v.Smma ?? double.NaN).ToArray()
            );
        var indicator = new QuanTAlib.Smma(period);
        return new(
            0,
            data.Closes.Select(v => indicator.Calc(new QuanTAlib.TValue(v, true, false)).Value)
                .ToArray()
        );
    }

    private static ComparisonSeries Reference(
        string id,
        CompetitorData data,
        int period,
        bool native
    )
    {
        var values = new double[data.Count];
        var seed = BigInteger.Zero;
        var grid = MoneyFlowReferenceArithmetic.Grid;
        decimal previousDecimal = 0;
        for (var i = 0; i < data.Count; i++)
        {
            var value = data.Closes[i];
            if (native && FirstSeed(id))
            {
                var input = (decimal)value;
                var alpha = 1m / period;
                previousDecimal =
                    i == 0 ? input : previousDecimal + alpha * (input - previousDecimal);
                values[i] = (double)previousDecimal;
            }
            else if (!FirstSeed(id) && i < period)
            {
                seed += MoneyFlowReferenceArithmetic.Units(value);
                values[i] = MoneyFlowReferenceArithmetic.Round(seed, grid * (i + 1));
            }
            else if (i == 0)
                values[i] = value;
            else if (native)
            {
                values[i] = MoneyFlowReferenceArithmetic.Divide(
                    MoneyFlowReferenceArithmetic.Add(
                        MoneyFlowReferenceArithmetic.Multiply(values[i - 1], period - 1),
                        value
                    ),
                    period
                );
            }
            else
            {
                var numerator =
                    MoneyFlowReferenceArithmetic.Units(values[i - 1]) * (period - 1)
                    + MoneyFlowReferenceArithmetic.Units(value);
                values[i] = MoneyFlowReferenceArithmetic.Round(numerator, grid * period);
            }
        }
        return new(First(id, period, data.Count), values);
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromCloses([3, 12, -6, 9, 18, -3, 4, 4, 4, -18, 20]);
}
