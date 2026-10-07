using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class MeanErrorComparison
{
    internal static readonly string[] Names = ["Sma", "Mad", "Mse", "Mape"];
    internal static readonly ComparisonPair[] Pairs =
    [
        new(
            "TaLib.Functions.AvgDev",
            "WindowMeanAbsoluteDeviation",
            TaLib,
            (d, p) => Ooples(d, p, false),
            (d, p) => Reference(d, p, false, false),
            CompetitorReference: (d, p) => Reference(d, p, false, true)
        ),
        new(
            "Skender.GetSmaAnalysis",
            "MovingAverageDiagnostics",
            Skender,
            (d, p) => Ooples(d, p, true),
            (d, p) => Reference(d, p, true, false),
            Names,
            CompetitorReference: (d, p) => Reference(d, p, true, true)
        ),
    ];

    private static ComparisonSeries Series(double?[][] values) =>
        new(
            Names
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

    private static ComparisonSeries Ooples(CompetitorData data, int period, bool details)
    {
        IIndicator indicator = details
            ? new MovingAverageDiagnostics(period)
            : new WindowMeanAbsoluteDeviation(period);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        if (!details)
            return new(Math.Min(period - 1, data.Count), run[indicator.Outputs[0]].ToArray());
        return Series(
            Enumerable
                .Range(0, 4)
                .Select(slot =>
                {
                    var values = run[indicator.Outputs[slot]].ToArray();
                    var present = run[indicator.Outputs[slot + 4]].ToArray();
                    return values.Select((v, i) => present[i] > 0 ? (double?)v : null).ToArray();
                })
                .ToArray()
        );
    }

    private static ComparisonSeries Skender(CompetitorData data, int period)
    {
        var rows = data.Quotes.GetSmaAnalysis(period).ToArray();
        return Series([
            rows.Select(r => r.Sma).ToArray(),
            rows.Select(r => r.Mad).ToArray(),
            rows.Select(r => r.Mse).ToArray(),
            rows.Select(r => r.Mape).ToArray(),
        ]);
    }

    private static ComparisonSeries TaLib(CompetitorData data, int period)
    {
        var packed = new double[data.Count];
        var code = Functions.AvgDev<double>(
            data.Closes,
            System.Range.All,
            packed,
            out var range,
            period
        );
        var first = Math.Min(period - 1, data.Count);
        if (data.Count == 1 && code == TALib.Core.RetCode.OutOfRangeParam)
            return new(first, [double.NaN]);
        if (code != TALib.Core.RetCode.Success)
            throw new InvalidOperationException("TA-Lib average deviation returned " + code);
        var (start, count) = range.GetOffsetAndLength(data.Count);
        if (count != data.Count - first || count > 0 && start != first)
            throw new InvalidOperationException("Unexpected average deviation alignment.");
        var values = Enumerable.Repeat(double.NaN, data.Count).ToArray();
        Array.Copy(packed, 0, values, start, count);
        return new(first, values);
    }

    private static ComparisonSeries Reference(
        CompetitorData data,
        int period,
        bool details,
        bool native
    )
    {
        var result = Enumerable.Range(0, 4).Select(_ => new double?[data.Count]).ToArray();
        for (var i = period - 1; i < data.Count; i++)
        {
            if (native)
                NativeWindow(data, period, i, details, result);
            else
                ExactWindow(data, period, i, details, result);
        }
        return details
            ? Series(result)
            : new(
                Math.Min(period - 1, data.Count),
                result[1].Select(v => v ?? double.NaN).ToArray()
            );
    }

    private static void ExactWindow(
        CompetitorData data,
        int period,
        int index,
        bool details,
        double?[][] results
    )
    {
        var window = data.Closes.Skip(index - period + 1).Take(period).Select(Units).ToArray();
        var sum = window.Aggregate(BigInteger.Zero, (a, b) => a + b);
        var mean = Round(sum, period * Grid);
        var deviations = window.Select(v => BigInteger.Abs(v - Units(mean))).ToArray();
        results[1][index] = Round(
            deviations.Aggregate(BigInteger.Zero, (a, b) => a + b),
            period * Grid
        );
        if (!details)
            return;
        results[0][index] = mean;
        results[2][index] = Round(
            deviations.Aggregate(BigInteger.Zero, (a, b) => a + b * b),
            period * Grid * Grid
        );
        if (window.Any(v => v.IsZero))
            return;
        // Independent product-denominator sum; production reduces the accumulator
        // after every term and uses common denominators rather than their product.
        var numerator = BigInteger.Zero;
        var denominator = BigInteger.One;
        for (var j = 0; j < period; j++)
        {
            var top = deviations[j] * window[j].Sign;
            var bottom = BigInteger.Abs(window[j]);
            var common = BigInteger.GreatestCommonDivisor(BigInteger.Abs(top), bottom);
            top /= common;
            bottom /= common;
            numerator = numerator * bottom + top * denominator;
            denominator *= bottom;
        }
        results[3][index] = Round(numerator, denominator * period);
    }

    private static void NativeWindow(
        CompetitorData data,
        int period,
        int index,
        bool details,
        double?[][] results
    )
    {
        var window = data
            .Closes.Skip(index - period + 1)
            .Take(period)
            .Select(v => details ? (double)(decimal)v : v)
            .ToArray();
        // Skender sums oldest first; TA-Lib walks each window backwards.
        if (!details)
            Array.Reverse(window);
        var mean = Divide(window.Aggregate(0d, Add), period);
        var differences = window.Select(v => Math.Abs(Subtract(v, mean))).ToArray();
        results[1][index] = Divide(differences.Aggregate(0d, Add), period);
        if (!details)
            return;
        results[0][index] = mean;
        results[2][index] = Divide(
            differences.Aggregate(0d, (a, b) => Add(a, Multiply(b, b))),
            period
        );
        if (window.Any(v => Math.Abs(v) <= 0))
            return;
        double relative = 0;
        for (var j = 0; j < period; j++)
            relative = Add(relative, Divide(differences[j], window[j]));
        results[3][index] = Divide(relative, period);
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromCloses([1, 3, 4, -1, -3, -5, 0, 0, 1, 3, 4, .1, .1, .1]);

    internal static CompetitorData RoundedMeanFixture() =>
        CompetitorData.FromCloses([
            Math.ScaleB(1, 53),
            Math.ScaleB(1, 53) + 2,
            Math.ScaleB(1, 53) + 4,
        ]);

    internal static CompetitorData CollapsedQuoteFixture() =>
        CompetitorData.FromCloses([.1, Math.BitIncrement(.1), .1, Math.BitIncrement(.1)]);
}
