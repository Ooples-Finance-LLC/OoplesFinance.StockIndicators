using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class AtrTrailingComparison
{
    internal static readonly string[] Names = ["Stop", "UpperBand", "LowerBand"];

    internal static ComparisonPair Pair(AtrTrailBasis basis, double multiplier = 3) =>
        new(
            basis == AtrTrailBasis.Midpoint ? "Skender.GetSuperTrend" : "Skender.GetAtrStop",
            nameof(SeededAtrTrailingStop),
            (d, p) => Native(d, p, multiplier, basis),
            (d, p) => Owned(d.IndicatorBars, p, multiplier, basis),
            (d, p) => Series(GridReference(d.IndicatorBars, p, multiplier, basis)),
            Names,
            CompetitorReference: (d, p) => Series(NativeReference(d, p, multiplier, basis)),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Series(double?[][] rows) =>
        RetrospectivePriceComparison.Series(Names, rows);

    internal static ComparisonSeries Native(
        CompetitorData d,
        int period,
        double multiplier,
        AtrTrailBasis basis
    )
    {
        if (basis == AtrTrailBasis.Midpoint)
        {
            var r = d.Quotes.GetSuperTrend(period, multiplier).ToArray();
            return Series([
                r.Select(v => (double?)v.SuperTrend).ToArray(),
                r.Select(v => (double?)v.UpperBand).ToArray(),
                r.Select(v => (double?)v.LowerBand).ToArray(),
            ]);
        }
        var a = d
            .Quotes.GetAtrStop(
                period,
                multiplier,
                basis == AtrTrailBasis.Close ? EndType.Close : EndType.HighLow
            )
            .ToArray();
        return Series([
            a.Select(v => (double?)v.AtrStop).ToArray(),
            a.Select(v => (double?)v.BuyStop).ToArray(),
            a.Select(v => (double?)v.SellStop).ToArray(),
        ]);
    }

    internal static ComparisonSeries Owned(
        Bar[] bars,
        int period,
        double multiplier,
        AtrTrailBasis basis
    )
    {
        var owner = new SeededAtrTrailingStop(period, multiplier, basis);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(owner)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return Series(
            Enumerable
                .Range(0, 3)
                .Select(j =>
                {
                    var values = run[owner.Outputs[j]].ToArray();
                    var flags = run[owner.Outputs[j + 3]].ToArray();
                    return values.Select((v, i) => flags[i] > 0 ? (double?)v : null).ToArray();
                })
                .ToArray()
        );
    }

    internal static double?[][] NativeReference(
        CompetitorData data,
        int period,
        double multiplier,
        AtrTrailBasis basis
    )
    {
        var bars = data.Quotes.ToArray();
        var result = Enumerable.Range(0, 3).Select(_ => new double?[bars.Length]).ToArray();
        double sum = 0,
            atr = 0,
            upper = 0,
            lower = 0;
        var bullish = true;
        for (var i = 1; i < bars.Length; i++)
        {
            var high = (double)bars[i].High;
            var low = (double)bars[i].Low;
            var close = (double)bars[i].Close;
            var previous = (double)bars[i - 1].Close;
            var range = Math.Max(
                high - low,
                Math.Max(Math.Abs(high - previous), Math.Abs(low - previous))
            );
            if (i <= period)
                sum += range;
            if (i < period)
                continue;
            atr = i == period ? sum / period : (atr * (period - 1) + range) / period;
            var mid = (high + low) / 2;
            var width = multiplier * atr;
            var ue =
                (
                    basis == AtrTrailBasis.Close ? close
                    : basis == AtrTrailBasis.HighLow ? high
                    : mid
                ) + width;
            var le =
                (
                    basis == AtrTrailBasis.Close ? close
                    : basis == AtrTrailBasis.HighLow ? low
                    : mid
                ) - width;
            if (i == period)
            {
                bullish = close >= (basis == AtrTrailBasis.Midpoint ? mid : previous);
                upper = ue;
                lower = le;
            }
            if (ue < upper || previous > upper)
                upper = ue;
            if (le > lower || previous < lower)
                lower = le;
            bullish = !(close <= (bullish ? lower : upper));
            result[0][i] = result[bullish ? 2 : 1][i] = (double)(decimal)(bullish ? lower : upper);
        }
        return result;
    }

    internal static double?[][] GridReference(
        Bar[] bars,
        int period,
        double multiplier,
        AtrTrailBasis basis
    )
    {
        var result = Enumerable.Range(0, 3).Select(_ => new double?[bars.Length]).ToArray();
        BigInteger sum = 0,
            atr = 0,
            upper = 0,
            lower = 0;
        var bullish = true;
        BigInteger R(BigInteger n, BigInteger d) => DirectionalComparison.RoundedUnits(n, d);
        for (var i = 1; i < bars.Length; i++)
        {
            var high = Units(bars[i].High);
            var low = Units(bars[i].Low);
            var close = Units(bars[i].Close);
            var previous = Units(bars[i - 1].Close);
            var range = R(
                BigInteger.Max(
                    high - low,
                    BigInteger.Max(BigInteger.Abs(high - previous), BigInteger.Abs(low - previous))
                ),
                1
            );
            if (i <= period)
                sum += range;
            if (i < period)
                continue;
            atr = R(i == period ? sum : atr * (period - 1) + range, period);
            var mid = R(high + low, 2);
            var width = R(atr * Units(multiplier), Grid);
            var ue = R(
                (
                    basis == AtrTrailBasis.Close ? close
                    : basis == AtrTrailBasis.HighLow ? high
                    : mid
                ) + width,
                1
            );
            var le = R(
                (
                    basis == AtrTrailBasis.Close ? close
                    : basis == AtrTrailBasis.HighLow ? low
                    : mid
                ) - width,
                1
            );
            if (i == period)
            {
                bullish = close >= (basis == AtrTrailBasis.Midpoint ? mid : previous);
                upper = ue;
                lower = le;
            }
            if (ue < upper || previous > upper)
                upper = ue;
            if (le > lower || previous < lower)
                lower = le;
            bullish = close > (bullish ? lower : upper);
            result[0][i] = result[bullish ? 2 : 1][i] = Round(bullish ? lower : upper, Grid);
        }
        return result;
    }
}
