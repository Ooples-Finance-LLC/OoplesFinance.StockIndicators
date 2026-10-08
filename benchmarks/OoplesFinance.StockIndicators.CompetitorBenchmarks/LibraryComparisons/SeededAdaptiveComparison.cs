using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class SeededAdaptiveComparison
{
    internal static readonly ComparisonPair[] Pairs = [Pair(false), Pair(true)];

    internal static ComparisonPair Pair(bool quan, int fast = 2, int slow = 30) =>
        new(
            quan ? "QuanTAlib.Kama" : "Skender.GetKama",
            nameof(SeededAdaptiveAverage),
            (d, p) => Native(d, p, fast, slow, quan),
            (d, p) => Owned(d.IndicatorBars, p, fast, slow, quan),
            (d, p) => Reference(d, p, fast, slow, quan, false),
            quan ? ["Kama"] : ["Kama", "ER"],
            MinimumInputCount: 0,
            CompetitorReference: (d, p) => Reference(d, p, fast, slow, quan, true),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Series(double?[][] r, bool quan) =>
        RetrospectivePriceComparison.Series(quan ? ["Kama"] : ["Kama", "ER"], quan ? [r[0]] : r);

    internal static ComparisonSeries Owned(Bar[] bars, int p, int fast, int slow, bool quan)
    {
        var indicator = new SeededAdaptiveAverage(p, fast, slow, quan, quan, !quan);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var r = new double?[2][];
        for (var j = 0; j < 2; j++)
        {
            var v = run[indicator.Outputs[j]].ToArray();
            var f = run[indicator.Outputs[j + 2]].ToArray();
            r[j] = v.Select((x, i) => f[i] > 0 ? (double?)x : null).ToArray();
        }
        return Series(r, quan);
    }

    private static ComparisonSeries Native(CompetitorData d, int p, int fast, int slow, bool quan)
    {
        if (quan)
        {
            var indicator = new QuanTAlib.Kama(p, fast, slow);
            return Series(
                [
                    d
                        .Closes.Select(v =>
                            (double?)indicator.Calc(new QuanTAlib.TValue(v, true, false)).Value
                        )
                        .ToArray(),
                ],
                true
            );
        }
        var r = d.Quotes.GetKama(p, fast, slow).ToArray();
        return Series([r.Select(v => v.Kama).ToArray(), r.Select(v => v.ER).ToArray()], false);
    }

    internal static ComparisonSeries Reference(
        CompetitorData d,
        int p,
        int fast,
        int slow,
        bool quan,
        bool native
    )
    {
        var prices = (native && !quan ? d.Quotes.Select(q => (double)q.Close) : d.Closes).ToArray();
        return Series(ReferenceValues(prices, p, fast, slow, quan, native), quan);
    }

    internal static double?[][] ReferenceValues(
        double[] prices,
        int p,
        int fast,
        int slow,
        bool quan,
        bool native,
        bool? resetFlat = null,
        bool fastFlat = false,
        long outputDelay = 0
    )
    {
        var r = Enumerable.Range(0, 2).Select(_ => new double?[prices.Length]).ToArray();
        var f = Divide(2, (double)(quan ? Math.Min(p, fast) : fast) + 1);
        var s = Divide(2, (double)slow + 1);
        var average = 0d;
        for (var i = 0; i < prices.Length; i++)
        {
            if (i < p)
                average = prices[i];
            else
            {
                var total = BigInteger.Zero;
                var nativeTotal = 0d;
                for (var j = i - p + 1; j <= i; j++)
                {
                    total += BigInteger.Abs(Units(prices[j]) - Units(prices[j - 1]));
                    if (native)
                        nativeTotal = Add(
                            nativeTotal,
                            Math.Abs(Subtract(prices[j], prices[j - 1]))
                        );
                }
                var flat = native ? nativeTotal == 0 : total.IsZero;
                var er = flat
                    ? fastFlat
                        ? 1
                        : 0
                    : native
                        ? Divide(Math.Abs(Subtract(prices[i], prices[i - p])), nativeTotal)
                        : Round(BigInteger.Abs(Units(prices[i]) - Units(prices[i - p])), total);
                var rate = native
                    ? Add(Multiply(er, Subtract(f, s)), s)
                    : Round(Units(er) * (Units(f) - Units(s)) + Units(s) * Grid, Grid * Grid);
                var square = Multiply(rate, rate);
                average =
                    (resetFlat ?? !quan) && flat ? prices[i]
                    : native ? Add(average, Multiply(square, Subtract(prices[i], average)))
                    : Round(
                        Units(average) * Grid + Units(square) * (Units(prices[i]) - Units(average)),
                        Grid * Grid
                    );
                r[1][i] = er;
            }
            if ((long)i >= (quan ? 0L : p - 1L) + outputDelay)
                r[0][i] = average;
        }
        return r;
    }
}
