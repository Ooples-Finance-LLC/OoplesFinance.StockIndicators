using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class SchaffCycleComparison
{
    internal static ComparisonPair Pair(int cycle = 10, int fast = 23, int slow = 50) =>
        new(
            "Skender.GetStc",
            nameof(SchaffTrendCycleSnapshot),
            (d, _) => Series(d.Quotes.GetStc(cycle, fast, slow).Select(r => r.Stc).ToArray()),
            (d, _) =>
                Series(
                    SchaffTrendCycleSnapshot.Calculate(d.IndicatorBars, cycle, fast, slow).ToArray()
                ),
            (d, _) => Series(Reference(d.Closes, cycle, fast, slow)),
            ["Stc"],
            MinimumInputCount: 0,
            CompetitorReference: (d, _) =>
                Series(
                    NativeReference(
                        d.Quotes.Select(q => (double)q.Close).ToArray(),
                        cycle,
                        fast,
                        slow
                    )
                ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Series(double?[] values) =>
        RetrospectivePriceComparison.Series(["Stc"], [values]);

    internal static double?[] NativeReference(double[] prices, int cycle, int fast, int slow)
    {
        var start = Math.Min(slow - 1, prices.Length);
        var macd = EmaDifferenceSignalComparison.NativeReference(prices, fast, slow, 1, false)[0];
        var bars = macd.Skip(start)
            .Select(
                (v, i) =>
                    new Bar(
                        DateTime.UnixEpoch.AddMinutes(i),
                        v ?? double.NaN,
                        v ?? double.NaN,
                        v ?? double.NaN,
                        v ?? double.NaN,
                        0
                    )
            )
            .ToArray();
        var result = new double?[prices.Length];
        WindowStochasticComparison
            .NativeReference(bars, cycle, 3, 1, false, 3, 2)[0]
            .CopyTo(result, start);
        return result;
    }

    private static BigInteger?[] Average(double[] prices, int period)
    {
        var output = new BigInteger?[prices.Length];
        if (prices.Length < period)
            return output;
        var value = DirectionalComparison.RoundedUnits(
            prices.Take(period).Aggregate(BigInteger.Zero, (n, v) => n + Units(v)),
            period
        );
        output[period - 1] = value;
        for (var i = period; i < prices.Length; i++)
        {
            value = DirectionalComparison.RoundedUnits(
                value * (period - 1) + 2 * Units(prices[i]),
                (long)period + 1
            );
            output[i] = value;
        }
        return output;
    }

    internal static double?[] Reference(double[] prices, int cycle, int fast, int slow)
    {
        var f = Average(prices, fast);
        var s = Average(prices, slow);
        var macd = new BigInteger[prices.Length];
        for (var i = slow - 1; i < prices.Length; i++)
            macd[i] = DirectionalComparison.RoundedUnits(f[i]!.Value - s[i]!.Value, 1);
        var raw = new double?[prices.Length];
        var result = new double?[prices.Length];
        var first = (long)slow + cycle - 2;
        for (var j = first; j < prices.Length; j++)
        {
            var i = (int)j;
            var selected = macd.Skip(i + 1 - cycle).Take(cycle).Order().ToArray();
            raw[i] =
                selected[0] == selected[^1]
                    ? 0
                    : Round(100 * (macd[i] - selected[0]), selected[^1] - selected[0]);
            if (j >= first + 2)
                result[i] = Round(
                    Units(raw[i]!.Value) + Units(raw[i - 1]!.Value) + Units(raw[i - 2]!.Value),
                    3 * Grid
                );
        }
        return result;
    }
}
