using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ChoppinessComparison
{
    internal static readonly IndicatorErrorBudget Budget = new(0, 4e-15, true);
    internal static readonly ComparisonPair Pair = new(
        "Skender.GetChop",
        nameof(WindowChoppinessIndex),
        (d, p) => VolumePriceComparison.Mask(d.Quotes.GetChop(p).Select(r => r.Chop).ToArray()),
        (d, p) => Owned(d.IndicatorBars, p),
        (d, p) => VolumePriceComparison.Mask(Reference(d.IndicatorBars, p)),
        CompetitorReference: (d, p) =>
            VolumePriceComparison.Mask(
                NativeReference(
                    d.Quotes.Select(q => new Bar(
                            q.Date,
                            (double)q.Open,
                            (double)q.High,
                            (double)q.Low,
                            (double)q.Close,
                            (double)q.Volume
                        ))
                        .ToArray(),
                    p
                )
            ),
        ErrorBudget: Budget
    );

    internal static ComparisonSeries Owned(Bar[] bars, int period)
    {
        var indicator = new WindowChoppinessIndex(period);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = run[indicator.Value].ToArray();
        var mask = run[indicator.IsDefined].ToArray();
        return VolumePriceComparison.Mask(
            values.Select((v, i) => mask[i] > 0 ? (double?)v : null).ToArray()
        );
    }

    // Independent fixed-point Horner log, with extra precision when the ratio is near one.
    internal static double Log(BigInteger numerator, BigInteger denominator)
    {
        if (numerator == denominator)
            return 0;
        var extra = Math.Max(
            0,
            (int)(
                denominator.GetBitLength() - BigInteger.Abs(numerator - denominator).GetBitLength()
            )
        );
        var scale = BigInteger.One << (256 + extra);
        var exponent = (int)(numerator.GetBitLength() - denominator.GetBitLength());
        if (
            exponent >= 0
                ? numerator < (denominator << exponent)
                : (numerator << -exponent) < denominator
        )
            exponent--;
        var n = exponent < 0 ? numerator << -exponent : numerator;
        var d = exponent > 0 ? denominator << exponent : denominator;
        BigInteger Series(BigInteger z)
        {
            var square = z * z / scale;
            var polynomial = scale / 241;
            for (var k = 119; k >= 0; k--)
                polynomial = scale / (2 * k + 1) + polynomial * square / scale;
            return 2 * z * polynomial / scale;
        }
        return Round(Series((n - d) * scale / (n + d)) + exponent * Series(scale / 3), scale);
    }

    internal static double?[] Reference(Bar[] bars, int period)
    {
        var result = new double?[bars.Length];
        for (var i = period; i < bars.Length; i++)
        {
            var intervals = Enumerable
                .Range(i - period + 1, period)
                .Select(j =>
                    (
                        High: Units(Math.Max(bars[j].High, bars[j - 1].Close)),
                        Low: Units(Math.Min(bars[j].Low, bars[j - 1].Close))
                    )
                )
                .ToArray();
            var span = intervals.Max(v => v.High) - intervals.Min(v => v.Low);
            if (span.IsZero)
                continue;
            var total = intervals.Aggregate(BigInteger.Zero, (s, v) => s + v.High - v.Low);
            if (total.IsZero)
            {
                result[i] = double.NegativeInfinity;
                continue;
            }
            result[i] = Multiply(100, Divide(Log(total, span), Log(period, 1)));
        }
        return result;
    }

    internal static double?[] NativeReference(Bar[] bars, int period)
    {
        var result = new double?[bars.Length];
        for (var i = period; i < bars.Length; i++)
        {
            var intervals = Enumerable
                .Range(i - period + 1, period)
                .Select(j =>
                    (
                        High: Math.Max(bars[j].High, bars[j - 1].Close),
                        Low: Math.Min(bars[j].Low, bars[j - 1].Close)
                    )
                )
                .Reverse()
                .ToArray();
            var span = Subtract(intervals.Max(v => v.High), intervals.Min(v => v.Low));
            if (span == 0)
                continue; // NOSONAR: Native formula makes exactly zero ranges absent.
            var sum = intervals.Select(v => Subtract(v.High, v.Low)).Aggregate(0d, Add);
            result[i] = Multiply(100, Divide(Math.Log(Divide(sum, span)), Math.Log(period)));
        }
        return result;
    }
}
