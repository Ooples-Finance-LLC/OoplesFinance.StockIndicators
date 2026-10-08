using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class MoneyFlowDetailComparison
{
    internal static readonly string[] Names = ["MoneyFlowMultiplier", "MoneyFlowVolume", "Cmf"];
    internal static readonly ComparisonPair Pair = new(
        "Skender.GetCmf",
        "MoneyFlowWithDetails",
        Native,
        Owned,
        (d, p) => Reference(d, p, false),
        Names,
        CompetitorReference: (d, p) => Reference(d, p, true)
    );

    private static ComparisonSeries Series(double?[][] values) =>
        new(
            Names
                .Select(
                    (name, slot) =>
                        new KeyValuePair<string, ComparisonOutput>(
                            name,
                            VolumePriceComparison.Nullable(values[slot])
                        )
                )
                .ToDictionary(kv => kv.Key, kv => kv.Value)
        );

    private static ComparisonSeries Native(CompetitorData data, int period)
    {
        var rows = data.Quotes.GetCmf(period).ToArray();
        return Series([
            rows.Select(r => r.MoneyFlowMultiplier).ToArray(),
            rows.Select(r => r.MoneyFlowVolume).ToArray(),
            rows.Select(r => r.Cmf).ToArray(),
        ]);
    }

    private static ComparisonSeries Owned(CompetitorData data, int period)
    {
        var indicator = new MoneyFlowWithDetails(period);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var fields = Enumerable
            .Range(0, 3)
            .Select(slot => run[indicator.Outputs[slot]].ToArray())
            .ToArray();
        var flags = run[indicator.CmfIsDefined].ToArray();
        return Series(
            fields
                .Select(
                    (row, slot) =>
                        row.Select((v, i) => slot < 2 || flags[i] > 0 ? (double?)v : null).ToArray()
                )
                .ToArray()
        );
    }

    private static ComparisonSeries Reference(CompetitorData data, int period, bool native)
    {
        var values = Enumerable.Range(0, 3).Select(_ => new double?[data.Count]).ToArray();
        for (var i = 0; i < data.Count; i++)
        {
            var high = data.Highs[i];
            var low = data.Lows[i];
            var close = data.Closes[i];
            var volume = data.Volumes[i];
            if (native)
            {
                high = Price(high);
                low = Price(low);
                close = Price(close);
                volume = Price(volume);
                var denominator = Subtract(high, low);
                var multiplier =
                    Math.Abs(denominator) > 0
                        ? Divide(Subtract(Subtract(close, low), Subtract(high, close)), denominator)
                        : 0;
                values[0][i] = multiplier;
                values[1][i] = Multiply(multiplier, volume);
            }
            else
            {
                var numerator = 2 * Units(close) - Units(high) - Units(low);
                var denominator = Units(high) - Units(low);
                values[0][i] = denominator.IsZero ? 0 : Round(numerator, denominator);
                values[1][i] = denominator.IsZero
                    ? 0
                    : Round(numerator * Units(volume), denominator * Grid);
            }
            if (i < period - 1)
                continue;
            if (native)
            {
                double flow = 0,
                    mass = 0;
                for (var j = i - period + 1; j <= i; j++)
                {
                    flow = Add(flow, values[1][j]!.Value);
                    mass = Add(mass, Price(data.Volumes[j]));
                }
                var averageVolume = Divide(mass, period);
                if (Math.Abs(averageVolume) > 0)
                    values[2][i] = Divide(Divide(flow, period), averageVolume);
            }
            else
            {
                var flow = BigInteger.Zero;
                var mass = BigInteger.Zero;
                for (var j = i - period + 1; j <= i; j++)
                {
                    flow += Units(values[1][j]!.Value);
                    mass += Units(data.Volumes[j]);
                }
                if (!mass.IsZero)
                    values[2][i] = Round(flow, mass);
            }
        }
        return Series(values);
    }

    private static double Price(double value) => (double)(decimal)value;

    internal static CompetitorData CollapsedRangeFixture()
    {
        var high = Math.BitIncrement(.1);
        return CompetitorData.FromOhlcv(
            [high, .1, high, .1],
            [high, high, high, high],
            [.1, .1, .1, .1],
            [high, .1, high, .1],
            [1, 2, 3, 4]
        );
    }
}
