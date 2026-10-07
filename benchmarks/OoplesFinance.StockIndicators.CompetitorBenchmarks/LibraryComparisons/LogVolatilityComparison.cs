using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class LogVolatilityComparison
{
    internal static readonly ComparisonPair[] Pairs = [Pair(false), Pair(true)];

    internal static ComparisonPair Pair(bool realized, bool annual = true) =>
        new(
            realized ? "QuanTAlib.Realized" : "QuanTAlib.Historical",
            nameof(WindowLogVolatility),
            (d, p) => Native(d, p, realized, annual),
            (d, p) => Owned(d.IndicatorBars, p, realized, annual),
            (d, p) => Series(Reference(d.Closes, p, realized, annual)),
            CompetitorReference: (d, p) =>
                Series(
                    NativeReference(d.Closes, p, realized, annual).Select(v => (double?)v).ToArray()
                ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Series(double?[] values) =>
        RetrospectivePriceComparison.Series(["Value"], [values]);

    internal static QuanTAlib.AbstractBase Indicator(int p, bool realized, bool annual) =>
        realized ? new QuanTAlib.Realized(p, annual) : new QuanTAlib.Historical(p, annual);

    private static ComparisonSeries Native(CompetitorData d, int p, bool realized, bool annual)
    {
        var indicator = Indicator(p, realized, annual);
        return Series(
            d.Closes.Select(v =>
                    (double?)indicator.Calc(new QuanTAlib.TValue(v, true, false)).Value
                )
                .ToArray()
        );
    }

    internal static ComparisonSeries Owned(Bar[] bars, int p, bool realized, bool annual)
    {
        var indicator = new WindowLogVolatility(p, realized, annual);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = run[indicator.Value].ToArray();
        var present = run[indicator.IsDefined].ToArray();
        return Series(values.Select((v, i) => present[i] > 0 ? (double?)v : null).ToArray());
    }

    internal static double?[] Reference(double[] prices, int p, bool realized, bool annual)
    {
        var result = Enumerable.Repeat((double?)0, prices.Length).ToArray();
        var returns = new List<BigInteger?>();
        var poisoned = false;
        for (var i = 0; i < prices.Length; i++)
        {
            var prior = i == 0 ? 0 : prices[i - 1];
            if (prior != 0) // NOSONAR: Exact zero boundary or certified equal rounded endpoints.
            {
                BigInteger? change = null;
                if (prices[i] != 0 && Math.Sign(prices[i]) == Math.Sign(prior)) // NOSONAR: Exact zero boundary or certified equal rounded endpoints.
                    change = Units(
                        ChoppinessComparison.Log(
                            BigInteger.Abs(Units(prices[i])),
                            BigInteger.Abs(Units(prior))
                        )
                    );
                else
                    poisoned = true;
                returns.Add(change);
            }
            else if (realized)
                continue;
            if (returns.Count < p)
                continue;
            var window = returns.Skip(returns.Count - p).ToArray();
            if (window.Any(v => !v.HasValue) || (realized && poisoned))
            {
                result[i] = null;
                continue;
            }
            var sum = window.Aggregate(BigInteger.Zero, (s, v) => s + v!.Value);
            var squares = window.Aggregate(BigInteger.Zero, (s, v) => s + v!.Value * v.Value);
            var n = realized ? squares : p * squares - sum * sum;
            var d = realized ? (BigInteger)p : (BigInteger)p * (p - 1);
            result[i] = DispersionReferenceArithmetic.Sqrt(n * (annual ? 252 : 1), d * Grid * Grid);
        }
        return result;
    }

    internal static double[] NativeReference(
        double[] prices,
        int p,
        bool realized,
        bool annual,
        bool[]? isNew = null
    )
    {
        var result = new double[prices.Length];
        var returns = new List<double>();
        var priceCount = 0;
        double previous = 0,
            squares = 0;
        for (var i = 0; i < prices.Length; i++)
        {
            var append = isNew?[i] ?? true;
            if (append || priceCount == 0)
                priceCount++;
            if ((realized || priceCount > 1) && previous != 0) // NOSONAR: Exact zero boundary or certified equal rounded endpoints.
            {
                var change = Math.Log(prices[i] / previous);
                if (realized && returns.Count == p)
                    squares -= Math.Pow(returns[0], 2);
                if (append || returns.Count == 0)
                {
                    if (returns.Count == p)
                        returns.RemoveAt(0);
                    returns.Add(change);
                }
                else
                    returns[^1] = change;
                if (realized)
                    squares += Math.Pow(change, 2);
            }
            if (returns.Count == p && (!realized || previous != 0)) // NOSONAR: Exact zero boundary or certified equal rounded endpoints.
            {
                var sum = 0d;
                foreach (var v in returns)
                    sum += v;
                var mean = sum / p;
                var variance = squares / p;
                if (!realized)
                {
                    var scatter = 0d;
                    foreach (var v in returns)
                        scatter += Math.Pow(v - mean, 2);
                    variance = scatter / (p - 1);
                }
                result[i] = Math.Sqrt(variance);
                if (annual)
                    result[i] *= Math.Sqrt(252);
            }
            previous = prices[i];
        }
        return result;
    }

    // Reuse the explicit native-nonfinite boundary verifier: invalid native log
    // results remain present and are never rewritten as absent or zero.
    internal static int Check(ComparisonPair pair, CompetitorData data, int period) =>
        SeededMomentumComparison.Check(pair, data, period);
}
