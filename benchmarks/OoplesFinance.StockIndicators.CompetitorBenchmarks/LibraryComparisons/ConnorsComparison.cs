using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ConnorsComparison
{
    internal static readonly string[] Names = ["Rsi", "RsiStreak", "PercentRank", "ConnorsRsi"];

    internal static ComparisonPair Pair(int rsi = 3, int streak = 2, int rank = 100) =>
        new(
            "Skender.GetConnorsRsi",
            nameof(ConnorsStrengthSnapshot),
            (d, _) => Native(d, rsi, streak, rank),
            (d, _) => Owned(d.IndicatorBars, rsi, streak, rank),
            (d, _) => Series(Reference(d.Closes, rsi, streak, rank, false)),
            Names,
            MinimumInputCount: 0,
            CompetitorReference: (d, _) =>
                Series(
                    Reference(
                        d.Quotes.Select(q => (double)q.Close).ToArray(),
                        rsi,
                        streak,
                        rank,
                        true
                    )
                ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Series(double?[][] rows) =>
        RetrospectivePriceComparison.Series(Names, rows);

    internal static ComparisonSeries Native(CompetitorData d, int rsi, int streak, int rank)
    {
        var rows = d.Quotes.GetConnorsRsi(rsi, streak, rank).ToArray();
        return Series([
            rows.Select(v => v.Rsi).ToArray(),
            rows.Select(v => v.RsiStreak).ToArray(),
            rows.Select(v => v.PercentRank).ToArray(),
            rows.Select(v => v.ConnorsRsi).ToArray(),
        ]);
    }

    internal static ComparisonSeries Owned(Bar[] bars, int rsi, int streak, int rank)
    {
        var rows = ConnorsStrengthSnapshot.Calculate(bars, rsi, streak, rank);
        return Series([
            rows.Select(v => v.Rsi).ToArray(),
            rows.Select(v => v.RsiStreak).ToArray(),
            rows.Select(v => v.PercentRank).ToArray(),
            rows.Select(v => v.ConnorsRsi).ToArray(),
        ]);
    }

    private static double?[] Rsi(double[] prices, int period, bool native)
    {
        if (!native)
        {
            var output = WilderStrengthComparison
                .OwnedReference(prices, period, WilderStrengthConvention.RsiHundredFlat)
                .Outputs["Value"];
            return output.Values.Select((v, i) => output.Present![i] ? (double?)v : null).ToArray();
        }
        var result = new double?[prices.Length];
        var (values, range) = WilderStrengthComparison.NativeReference(
            prices,
            period,
            WilderStrengthConvention.RsiHundredFlat
        );
        for (var i = 0; i < values.Length; i++)
            result[range.Start.Value + i] = values[i];
        return result;
    }

    internal static double?[][] Reference(
        double[] prices,
        int rsi,
        int streak,
        int rank,
        bool native
    )
    {
        var rows = Enumerable.Range(0, 4).Select(_ => new double?[prices.Length]).ToArray();
        rows[0] = Rsi(prices, rsi, native);
        // Count the terminal run directly, independently of the production running streak.
        var streaks = new double[Math.Max(0, prices.Length - 1)];
        for (var i = 1; i < prices.Length; i++)
        {
            var direction = prices[i].CompareTo(prices[i - 1]);
            if (direction == 0)
                continue;
            for (var j = i; j > 0 && prices[j].CompareTo(prices[j - 1]) == direction; j--)
                streaks[i - 1] += direction;
        }
        var streakValues = Rsi(streaks, streak, native);
        var gain = new double[prices.Length];
        if (native)
            for (var i = 1; i < prices.Length; i++)
                gain[i] =
                    prices[i - 1] > 0
                        ? Divide(Subtract(prices[i], prices[i - 1]), prices[i - 1])
                        : double.NaN;
        for (var i = 0; i < prices.Length; i++)
        {
            if (i >= (long)streak + 2)
                rows[1][i] = streakValues[i - 1];
            if (i >= rank)
            {
                var lower = 0;
                for (var j = i - rank; j < i; j++)
                {
                    if (
                        native
                            ? gain[j] < gain[i]
                            : prices[i - 1] > 0
                                && (
                                    j == 0
                                        ? prices[i] > prices[i - 1]
                                        : prices[j - 1] > 0
                                            && Units(prices[j]) * Units(prices[i - 1])
                                                < Units(prices[i]) * Units(prices[j - 1])
                                )
                    )
                        lower++;
                }
                rows[2][i] = 100L * lower / rank;
            }
            if (
                i >= (long)Math.Max(rsi, Math.Max(streak, rank)) + 1
                && rows[0][i].HasValue
                && rows[1][i].HasValue
                && rows[2][i].HasValue
            )
            {
                var a = rows[0][i]!.Value;
                var b = rows[1][i]!.Value;
                var c = rows[2][i]!.Value;
                rows[3][i] = native
                    ? Divide(Add(Add(a, b), c), 3)
                    : Round(Units(a) + Units(b) + Units(c), 3 * Grid);
            }
        }
        return rows;
    }
}
