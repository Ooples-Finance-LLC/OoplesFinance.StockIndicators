using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class JurikComparison
{
    internal static ComparisonPair Pair(double phase = 0, int shortPeriod = 10) =>
        new(
            "QuanTAlib.Jma",
            nameof(JurikAdaptiveSnapshot),
            (d, p) =>
            {
                var model = new QuanTAlib.Jma(p, phase, shortPeriod);
                return new(
                    0,
                    d.Closes.Select(v => model.Calc(new QuanTAlib.TValue(v, true, false)).Value)
                        .ToArray()
                );
            },
            (d, p) =>
                new(
                    0,
                    JurikAdaptiveSnapshot
                        .Calculate(d.IndicatorBars, p, phase, shortPeriod)
                        .ToArray()
                ),
            (d, p) => new(0, Reference(d.Closes, p, phase, shortPeriod, false)),
            CompetitorReference: (d, p) => new(0, Reference(d.Closes, p, phase, shortPeriod, true)),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static double[] Reference(
        double[] prices,
        int period,
        double phase,
        int shortPeriod,
        bool native
    )
    {
        var result = new double[prices.Length];
        if (prices.Length == 0)
            return result;
        BigInteger Stage(BigInteger n, BigInteger d) => DirectionalComparison.RoundedUnits(n, d);
        BigInteger Sum(BigInteger a, BigInteger b) =>
            native ? Units(Round(a, Grid) + Round(b, Grid)) : Stage(a + b, 1);
        BigInteger Difference(BigInteger a, BigInteger b) =>
            native ? Units(Round(a, Grid) - Round(b, Grid)) : a - b;
        BigInteger Product(BigInteger a, double factor) =>
            native ? Units(Round(a, Grid) * factor) : a * Units(factor);
        BigInteger Weighted(BigInteger a, BigInteger b, double w) =>
            native
                ? Sum(Product(a, 1 - w), Product(b, w))
                : Stage(a * (Grid - Units(w)) + b * Units(w), Grid);
        BigInteger Increment(BigInteger a, BigInteger b, double w) =>
            native ? Sum(a, Product(b, w)) : Stage(a * Grid + Product(b, w), Grid);
        var beta = .45 * (period - 1) / (.45 * (period - 1) + 2);
        var len = Math.Max(Math.Log(Math.Sqrt(period - 1)) / Math.Log(2) + 2, 0);
        var pwr = Math.Max(len - 2, .5);
        var gain = Math.Clamp(phase * .01 + 1.5, .5, 2.5) + 1;
        var bandH = BigInteger.Zero;
        var bandL = BigInteger.Zero;
        var ma = Units(prices[0]);
        var jma = ma;
        BigInteger d0 = 0,
            d1 = 0,
            sum = 0,
            average = 0;
        var volatilities = new List<BigInteger>();
        result[0] = prices[0];
        for (var i = 1; i < prices.Length; i++)
        {
            var window = prices
                .Skip(Math.Max(0, i - period + 1))
                .Take(Math.Min(i + 1, period))
                .ToArray();
            var high = Units(window.Max());
            var low = Units(window.Min());
            var dh = Difference(high, bandH);
            var dl = Difference(low, bandL);
            var volatility = BigInteger.Max(BigInteger.Abs(dh), BigInteger.Abs(dl));
            volatilities.Add(volatility);
            sum = Increment(
                sum,
                Difference(volatility, volatilities[Math.Max(0, volatilities.Count - shortPeriod)]),
                .1
            );
            average = Increment(
                average,
                Difference(sum, average),
                2 / (Math.Max(4d * period, 30) + 1)
            );
            var relative =
                average.Sign <= 0 ? 0
                : native ? Round(volatility, Grid) / Round(average, Grid)
                : Round(volatility, average);
            relative = Math.Min(Math.Max(relative, 1), Math.Pow(len, 1 / pwr));
            var power = Math.Pow(relative, pwr);
            var len2 = Math.Sqrt(.5 * (period - 1)) * len;
            var kv = Math.Pow(len2 / (len2 + 1), Math.Sqrt(power));
            bandH = dh.Sign > 0 ? high : Increment(high, -dh, kv);
            bandL = dl.Sign < 0 ? low : Increment(low, -dl, kv);
            var alpha = Math.Pow(beta, power);
            var input = Units(prices[i]);
            ma = Weighted(input, ma, alpha);
            d0 = Weighted(Difference(input, ma), d0, beta);
            var ma2 = Increment(ma, d0, gain);
            d1 = native
                ? Sum(
                    Product(Difference(ma2, jma), (1 - alpha) * (1 - alpha)),
                    Product(d1, alpha * alpha)
                )
                : Stage(
                    Units((1 - alpha) * (1 - alpha)) * (ma2 - jma) + Units(alpha * alpha) * d1,
                    Grid
                );
            jma = Sum(jma, d1);
            result[i] = Round(jma, Grid);
        }
        return result;
    }
}
