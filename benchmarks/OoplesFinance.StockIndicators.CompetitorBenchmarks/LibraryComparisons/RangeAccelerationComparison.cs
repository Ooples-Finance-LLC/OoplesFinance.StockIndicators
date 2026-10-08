using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class RangeAccelerationComparison
{
    internal static readonly string[] Names = ["Upper", "Middle", "Lower"];
    internal static readonly ComparisonPair Pair = new(
        "TaLib.Functions.Accbands",
        nameof(RangeAccelerationBands),
        Native,
        (d, p) => Owned(d.IndicatorBars, p),
        Reference,
        Names,
        MinimumInputCount: 2,
        CompetitorReference: NativeReference,
        ErrorBudget: IndicatorErrorBudget.Exact
    );

    internal static ComparisonSeries Series(double?[][] rows) =>
        RetrospectivePriceComparison.Series(Names, rows);

    internal static ComparisonSeries Owned(Bar[] bars, int period)
    {
        var indicator = new RangeAccelerationBands(period);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var rows = new double?[3][];
        for (var j = 0; j < 3; j++)
        {
            var v = run[indicator.Outputs[j]].ToArray();
            var f = run[indicator.Outputs[j + 3]].ToArray();
            rows[j] = v.Select((x, i) => f[i] > 0 ? (double?)x : null).ToArray();
        }
        return Series(rows);
    }

    private static ComparisonSeries Native(CompetitorData d, int p)
    {
        var rows = Enumerable.Range(0, 3).Select(_ => new double?[d.Count]).ToArray();
        if (d.Count == 0)
            return Series(rows);
        var output = Enumerable.Range(0, 3).Select(_ => new double[d.Count]).ToArray();
        var code = Functions.Accbands<double>(
            d.Highs,
            d.Lows,
            d.Closes,
            System.Range.All,
            output[0],
            output[1],
            output[2],
            out var range,
            p
        );
        if (code != TALib.Core.RetCode.Success)
            throw new InvalidOperationException("TA Accbands failed: " + code);
        for (var j = 0; j < 3; j++)
        for (var i = range.Start.Value; i < range.End.Value; i++)
            rows[j][i] = output[j][i - range.Start.Value];
        return Series(rows);
    }

    internal static ComparisonSeries Reference(CompetitorData d, int p)
    {
        var rows = Enumerable.Range(0, 3).Select(_ => new double?[d.Count]).ToArray();
        var input = Enumerable.Range(0, 3).Select(_ => new BigInteger[d.Count]).ToArray();
        for (var i = 0; i < d.Count; i++)
        {
            var h = Units(d.Highs[i]);
            var l = Units(d.Lows[i]);
            var sum = h + l;
            input[0][i] = sum.IsZero
                ? h
                : DirectionalComparison.RoundedUnits(
                    h * (5 * h - 3 * l) * sum.Sign,
                    BigInteger.Abs(sum)
                );
            input[1][i] = Units(d.Closes[i]);
            input[2][i] = sum.IsZero
                ? l
                : DirectionalComparison.RoundedUnits(
                    l * (5 * l - 3 * h) * sum.Sign,
                    BigInteger.Abs(sum)
                );
            if (i < p - 1)
                continue;
            for (var j = 0; j < 3; j++)
                rows[j][i] = Round(
                    input[j].Skip(i - p + 1).Take(p).Aggregate(BigInteger.Zero, (s, v) => s + v),
                    Grid * p
                );
        }
        return Series(rows);
    }

    private static ComparisonSeries NativeReference(CompetitorData d, int p)
    {
        var rows = Enumerable.Range(0, 3).Select(_ => new double?[d.Count]).ToArray();
        var values = NativePacked(d.Highs, d.Lows, d.Closes, p, 0, d.Count - 1);
        for (var j = 0; j < 3; j++)
        for (var i = p - 1; i < d.Count; i++)
            rows[j][i] = values[j][i - p + 1];
        return Series(rows);
    }

    // Native rolling sums add the incoming value, publish, then remove the oldest.
    // Generic IEEE operations preserve the float overload's stages as well.
    internal static T[][] NativePacked<T>(T[] high, T[] low, T[] close, int p, int start, int end)
        where T : IFloatingPointIeee754<T>
    {
        start = Math.Max(start, p - 1);
        if (start > end)
            return
            [
                [],
                [],
                [],
            ];
        var begin = start - p + 1;
        var rows = Enumerable.Range(0, 3).Select(_ => new T[end - begin + 1]).ToArray();
        for (var i = begin; i <= end; i++)
        {
            var h = high[i];
            var l = low[i];
            var sum = h + l;
            var a = T.IsZero(sum) ? T.Zero : T.CreateChecked(4) * (h - l) / sum;
            rows[0][i - begin] = h * (T.One + a);
            rows[1][i - begin] = close[i];
            rows[2][i - begin] = l * (T.One - a);
        }
        var result = Enumerable.Range(0, 3).Select(_ => new T[end - start + 1]).ToArray();
        for (var j = 0; j < 3; j++)
        {
            var total = T.Zero;
            for (var i = 0; i < p - 1; i++)
                total += rows[j][i];
            for (var i = p - 1; i < rows[j].Length; i++)
            {
                total += rows[j][i];
                var value = total;
                total -= rows[j][i - p + 1];
                result[j][i - p + 1] = value / T.CreateChecked(p);
            }
        }
        return result;
    }
}
