using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class VolatilityStopComparison
{
    internal static readonly string[] Names = ["Sar", "UpperBand", "LowerBand", "IsStop"];

    internal static ComparisonPair Pair(double multiplier = 3) =>
        new(
            "Skender.GetVolatilityStop",
            nameof(VolatilityStopSnapshot),
            (d, p) => Native(d, p, multiplier),
            (d, p) => Owned(d.IndicatorBars, p, multiplier),
            (d, p) => Series(Reference(d.IndicatorBars, p, multiplier, false)),
            Names,
            CompetitorReference: (d, p) =>
                Series(
                    Reference(
                        d.Quotes.Select(q => new Bar(
                                q.Date,
                                (double)q.Open,
                                (double)q.High,
                                (double)q.Low,
                                (double)q.Close,
                                (double)q.Volume
                            ))
                            .ToArray(),
                        p,
                        multiplier,
                        true
                    )
                ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Series(double?[][] rows) =>
        RetrospectivePriceComparison.Series(Names, rows);

    internal static ComparisonSeries Native(CompetitorData data, int period, double multiplier)
    {
        var rows = data.Quotes.GetVolatilityStop(period, multiplier).ToArray();
        return Series([
            rows.Select(r => r.Sar).ToArray(),
            rows.Select(r => r.UpperBand).ToArray(),
            rows.Select(r => r.LowerBand).ToArray(),
            rows.Select(r => r.IsStop.HasValue ? (double?)(r.IsStop.Value ? 1 : 0) : null)
                .ToArray(),
        ]);
    }

    internal static ComparisonSeries Owned(Bar[] bars, int period, double multiplier)
    {
        var rows = VolatilityStopSnapshot.Calculate(bars, period, multiplier);
        return Series([
            rows.Select(r => r.Sar).ToArray(),
            rows.Select(r => r.UpperBand).ToArray(),
            rows.Select(r => r.LowerBand).ToArray(),
            rows.Select(r => r.IsStop.HasValue ? (double?)(r.IsStop.Value ? 1 : 0) : null)
                .ToArray(),
        ]);
    }

    internal static double?[][] Reference(Bar[] bars, int period, double multiplier, bool native)
    {
        var output = Enumerable.Range(0, 4).Select(_ => new double?[bars.Length]).ToArray();
        if (bars.Length <= period)
            return output;
        var atr = new BigInteger[bars.Length];
        var nativeAtr = new double[bars.Length];
        BigInteger sum = 0;
        double nativeSum = 0;
        BigInteger R(BigInteger n, BigInteger d) => DirectionalComparison.RoundedUnits(n, d);
        for (var i = 1; i < bars.Length; i++)
        {
            if (native)
            {
                var tr = Math.Max(
                    bars[i].High - bars[i].Low,
                    Math.Max(
                        Math.Abs(bars[i].High - bars[i - 1].Close),
                        Math.Abs(bars[i].Low - bars[i - 1].Close)
                    )
                );
                if (i <= period)
                    nativeSum += tr;
                if (i >= period)
                    nativeAtr[i] =
                        i == period
                            ? nativeSum / period
                            : (nativeAtr[i - 1] * (period - 1) + tr) / period;
            }
            else
            {
                var tr = R(
                    BigInteger.Max(
                        Units(bars[i].High) - Units(bars[i].Low),
                        BigInteger.Max(
                            BigInteger.Abs(Units(bars[i].High) - Units(bars[i - 1].Close)),
                            BigInteger.Abs(Units(bars[i].Low) - Units(bars[i - 1].Close))
                        )
                    ),
                    1
                );
                if (i <= period)
                    sum += tr;
                if (i >= period)
                    atr[i] = R(i == period ? sum : atr[i - 1] * (period - 1) + tr, period);
            }
        }
        var bullish = bars[period - 1].Close > bars[0].Close;
        var extreme = bullish
            ? bars.Take(period).Max(b => b.Close)
            : bars.Take(period).Min(b => b.Close);
        var firstStop = -1;
        for (var i = period; i < bars.Length; i++)
        {
            var price = bars[i].Close;
            var reverse = false;
            if (i > period)
            {
                if (native)
                {
                    var stop = bullish
                        ? extreme - nativeAtr[i - 1] * multiplier
                        : extreme + nativeAtr[i - 1] * multiplier;
                    output[0][i] = output[bullish ? 2 : 1][i] = stop;
                    reverse = bullish ? price < stop : price > stop;
                }
                else
                {
                    var width = R(atr[i - 1] * Units(multiplier), Grid);
                    var stop = R(bullish ? Units(extreme) - width : Units(extreme) + width, 1);
                    output[0][i] = output[bullish ? 2 : 1][i] = Round(stop, Grid);
                    reverse = bullish ? Units(price) < stop : Units(price) > stop;
                }
            }
            output[3][i] = reverse ? 1 : 0;
            if (reverse)
            {
                if (firstStop < 0)
                    firstStop = i;
                extreme = price;
                bullish = !bullish;
            }
            else
                extreme = bullish ? Math.Max(extreme, price) : Math.Min(extreme, price);
        }
        foreach (var row in output)
            for (var i = 0; i <= firstStop; i++)
                row[i] = null;
        return output;
    }
}
