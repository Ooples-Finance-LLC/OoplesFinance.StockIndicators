using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class RetrospectivePriceComparison
{
    internal static readonly string[] DpoNames = ["Dpo", "Sma"],
        FractalNames = ["Bear", "Bull"],
        ChaosNames = ["Upper", "Lower"];
    internal static readonly ComparisonPair[] Pairs = [DpoPair, FractalPair(), ChaosPair];
    internal static ComparisonPair DpoPair =>
        new(
            "Skender.GetDpo",
            nameof(DetrendedPriceSnapshot),
            (d, p) =>
            {
                var rows = d.Quotes.GetDpo(p).ToArray();
                return Series(
                    DpoNames,
                    [rows.Select(r => r.Dpo).ToArray(), rows.Select(r => r.Sma).ToArray()]
                );
            },
            (d, p) => OwnedDpo(d.IndicatorBars, p),
            (d, p) => DpoReference(d.Closes, p, false),
            DpoNames,
            CompetitorReference: (d, p) =>
                DpoReference(d.Quotes.Select(q => (double)q.Close).ToArray(), p, true),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonPair FractalPair(
        int? left = null,
        int? right = null,
        bool useClose = false
    ) =>
        new(
            "Skender.GetFractal",
            nameof(FractalSnapshot),
            (d, p) =>
            {
                var rows = d
                    .Quotes.GetFractal(
                        left ?? p,
                        right ?? p,
                        useClose ? EndType.Close : EndType.HighLow
                    )
                    .ToArray();
                return Series(
                    FractalNames,
                    [
                        rows.Select(r => (double?)r.FractalBear).ToArray(),
                        rows.Select(r => (double?)r.FractalBull).ToArray(),
                    ]
                );
            },
            (d, p) => OwnedFractal(d.IndicatorBars, left ?? p, right ?? p, useClose),
            (d, p) => FractalReference(d, left ?? p, right ?? p, useClose, false, false),
            FractalNames,
            CompetitorReference: (d, p) =>
                FractalReference(d, left ?? p, right ?? p, useClose, false, true),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonPair ChaosPair =>
        new(
            "Skender.GetFcb",
            "FractalSnapshot.ChaosBands",
            (d, p) =>
            {
                var rows = d.Quotes.GetFcb(p).ToArray();
                return Series(
                    ChaosNames,
                    [
                        rows.Select(r => (double?)r.UpperBand).ToArray(),
                        rows.Select(r => (double?)r.LowerBand).ToArray(),
                    ]
                );
            },
            (d, p) => OwnedChaos(d.IndicatorBars, p),
            (d, p) => FractalReference(d, p, p, false, true, false),
            ChaosNames,
            CompetitorReference: (d, p) => FractalReference(d, p, p, false, true, true),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Series(string[] names, double?[][] values) =>
        new(
            names
                .Select(
                    (name, slot) =>
                        new KeyValuePair<string, ComparisonOutput>(
                            name,
                            new(
                                0,
                                values[slot].Select(v => v ?? double.NaN).ToArray(),
                                values[slot].Select(v => v.HasValue).ToArray()
                            )
                        )
                )
                .ToDictionary(kv => kv.Key, kv => kv.Value)
        );

    internal static ComparisonSeries OwnedDpo(Bar[] bars, int period)
    {
        var rows = DetrendedPriceSnapshot.Calculate(bars, period);
        return Series(
            DpoNames,
            [rows.Select(r => r.Dpo).ToArray(), rows.Select(r => r.Sma).ToArray()]
        );
    }

    internal static ComparisonSeries OwnedFractal(Bar[] bars, int left, int right, bool close)
    {
        var rows = FractalSnapshot.Calculate(bars, left, right, close);
        return Series(
            FractalNames,
            [rows.Select(r => r.Bear).ToArray(), rows.Select(r => r.Bull).ToArray()]
        );
    }

    internal static ComparisonSeries OwnedChaos(Bar[] bars, int period)
    {
        var rows = FractalSnapshot.ChaosBands(bars, period);
        return Series(
            ChaosNames,
            [rows.Select(r => r.Upper).ToArray(), rows.Select(r => r.Lower).ToArray()]
        );
    }

    internal static ComparisonSeries DpoReference(double[] prices, int period, bool native)
    {
        var means = new double?[prices.Length];
        var values = new double?[prices.Length];
        for (var center = 0; center < prices.Length; center++)
        {
            var end = (long)center + period / 2 + 1;
            if (end >= prices.Length || end < period - 1)
                continue;
            var window = prices.Skip((int)end - period + 1).Take(period).ToArray();
            means[center] = native
                ? Divide(window.Aggregate(0d, Add), period)
                : Round(
                    window.Aggregate(BigInteger.Zero, (sum, value) => sum + Units(value)),
                    period * Grid
                );
            values[center] = Subtract(prices[center], means[center]!.Value);
        }
        return Series(DpoNames, [values, means]);
    }

    internal static ComparisonSeries FractalReference(
        CompetitorData d,
        int left,
        int right,
        bool close,
        bool chaos,
        bool native
    )
    {
        var high = native
            ? d.Quotes.Select(q => Units((double)(close ? q.Close : q.High))).ToArray()
            : (close ? d.Closes : d.Highs).Select(Units).ToArray();
        var low = native
            ? d.Quotes.Select(q => Units((double)(close ? q.Close : q.Low))).ToArray()
            : (close ? d.Closes : d.Lows).Select(Units).ToArray();
        // Native decimal comparisons are independent of conversion to the published double.
        var highValues = native ? d.Quotes.Select(q => close ? q.Close : q.High).ToArray() : null;
        var lowValues = native ? d.Quotes.Select(q => close ? q.Close : q.Low).ToArray() : null;
        var upper = new double?[d.Count];
        var lower = new double?[d.Count];
        for (var i = left; i < (long)d.Count - right; i++)
        {
            var positions = Enumerable
                .Range(i - left, left + right + 1)
                .Where(j => j != i)
                .ToArray();
            var isHigh = positions.All(j =>
                native ? highValues![i] > highValues[j] : high[i] > high[j]
            );
            var isLow = positions.All(j => native ? lowValues![i] < lowValues[j] : low[i] < low[j]);
            if (isHigh)
                upper[i] = native ? (double)highValues![i] : Round(high[i], Grid);
            if (isLow)
                lower[i] = native ? (double)lowValues![i] : Round(low[i], Grid);
        }
        if (!chaos)
            return Series(FractalNames, [upper, lower]);
        var confirmedUpper = new double?[d.Count];
        var confirmedLower = new double?[d.Count];
        for (var end = 0; end < d.Count; end++)
        {
            // Scan all confirmed centers independently of the production carry-forward algorithm.
            for (var center = left; center <= (long)end - right; center++)
            {
                if (upper[center].HasValue)
                    confirmedUpper[end] = upper[center];
                if (lower[center].HasValue)
                    confirmedLower[end] = lower[center];
            }
        }
        return Series(ChaosNames, [confirmedUpper, confirmedLower]);
    }

    internal static CompetitorData Fixture()
    {
        double[] high = [3, 4, 9, 4, 3, 5, 8, 8, 4, 3, 2, 6, 4, 3, 2, 1, 5];
        double[] low = [1, 0, 2, 1, -3, 0, 1, 2, 0, -4, -1, 0, -2, -5, -2, -1, 0];
        var close = high.Zip(low, (h, l) => (h + l) / 2).ToArray();
        return CompetitorData.FromOhlcv(close, high, low, close, new double[high.Length]);
    }
}
