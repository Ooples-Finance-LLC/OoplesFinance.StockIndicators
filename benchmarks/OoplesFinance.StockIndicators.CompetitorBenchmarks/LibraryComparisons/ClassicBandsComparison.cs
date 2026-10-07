using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ClassicBandsComparison
{
    internal static readonly string[] Names = ["UpperBand", "MiddleBand", "LowerBand"];

    internal static ComparisonPair Pair(
        ClassicAverageMethod method = ClassicAverageMethod.Sma,
        double up = 2,
        double down = 2,
        bool first = false,
        int suppression = 0
    ) =>
        new(
            "TaLib.Functions.Bbands",
            nameof(ClassicDeviationBands),
            (d, p) => Native(d, p, method, up, down),
            (d, p) => Owned(d.IndicatorBars, p, method, up, down, first, suppression),
            (d, p) => Series(Reference(d.Closes, p, method, up, down, first, suppression)),
            Names,
            MinimumInputCount: 2,
            CompetitorReference: (d, p) =>
                NativeReference(d.Closes, p, method, up, down, first, suppression),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Series(double?[][] values) =>
        new(
            Names
                .Select(
                    (name, j) =>
                        new KeyValuePair<string, ComparisonOutput>(
                            name,
                            new(
                                0,
                                values[j].Select(v => v ?? double.NaN).ToArray(),
                                values[j].Select(v => v.HasValue).ToArray()
                            )
                        )
                )
                .ToDictionary(v => v.Key, v => v.Value)
        );

    internal static ComparisonSeries Owned(
        Bar[] bars,
        int p,
        ClassicAverageMethod method,
        double up,
        double down,
        bool first,
        int suppression
    )
    {
        var indicator = new ClassicDeviationBands(p, up, down, method, first, suppression);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return Series(
            Enumerable
                .Range(0, 3)
                .Select(j =>
                {
                    var v = run[indicator.Outputs[j]].ToArray();
                    var flags = run[indicator.Outputs[j + 3]].ToArray();
                    return v.Select((x, i) => flags[i] > 0 ? (double?)x : null).ToArray();
                })
                .ToArray()
        );
    }

    private static ComparisonSeries Native(
        CompetitorData d,
        int p,
        ClassicAverageMethod method,
        double up,
        double down
    )
    {
        var result = Enumerable.Range(0, 3).Select(_ => new double?[d.Count]).ToArray();
        if (d.Count == 0)
            return Series(result);
        var output = Enumerable.Range(0, 3).Select(_ => new double[d.Count]).ToArray();
        var code = Functions.Bbands<double>(
            d.Closes,
            System.Range.All,
            output[0],
            output[1],
            output[2],
            out var range,
            p,
            up,
            down,
            (TaCore.MAType)method
        );
        if (code != TaCore.RetCode.Success)
            throw new InvalidOperationException("TA BBANDS: " + code);
        for (var j = 0; j < 3; j++)
        for (var i = range.Start.Value; i < range.End.Value; i++)
            result[j][i] = output[j][i - range.Start.Value];
        return Series(result);
    }

    private static ComparisonSeries NativeReference(
        double[] input,
        int p,
        ClassicAverageMethod method,
        double up,
        double down,
        bool first,
        int suppression
    )
    {
        var r = NativePacked(input, p, method, up, down, first, suppression, 0, input.Length - 1);
        var result = Enumerable.Range(0, 3).Select(_ => new double?[input.Length]).ToArray();
        for (var j = 0; j < 3; j++)
        for (var i = 0; i < r.Values[j].Length; i++)
            result[j][r.Start + i] = r.Values[j][i];
        return Series(result);
    }

    internal static (int Start, T[][] Values) NativePacked<T>(
        T[] input,
        int p,
        ClassicAverageMethod method,
        double up,
        double down,
        bool first,
        int suppression,
        int requested,
        int end
    )
        where T : IFloatingPointIeee754<T>
    {
        var center = ClassicNativeReference.Packed(
            input,
            p,
            method,
            first,
            suppression,
            requested,
            end
        );
        var averageStart = Math.Max(
            requested,
            ClassicAverageComparison.First(p, method, suppression)
        );
        var firstOutput = Math.Max(averageStart, p - 1L);
        if (center.Length == 0 || firstOutput > end)
            return (
                0,
                [
                    [],
                    [],
                    [],
                ]
            );
        var start = (int)firstOutput;
        var count = end - start + 1;
        var result = Enumerable.Range(0, 3).Select(_ => new T[count]).ToArray();
        var sum = T.Zero;
        var squares = T.Zero;
        for (var i = start - p + 1; i < start; i++)
        {
            sum += input[i];
            squares += input[i] * input[i];
        }
        var period = T.CreateChecked(p);
        var upper = T.CreateChecked(up);
        var lower = T.CreateChecked(down);
        for (var i = start; i <= end; i++)
        {
            sum += input[i];
            squares += input[i] * input[i];
            var mean = method == ClassicAverageMethod.Sma ? center[i - start] : sum / period;
            var second = squares / period;
            sum -= input[i - p + 1];
            squares -= input[i - p + 1] * input[i - p + 1];
            var variance = second - mean * mean;
            var deviation = variance > T.Zero ? T.Sqrt(variance) : T.Zero;
            // Native changes the reported start for a longer deviation window,
            // but retains the original packed MA prefix (notably MAMA p>33).
            var middle = center[i - start];
            result[1][i - start] = middle;
            result[0][i - start] = middle + (upper.Equals(T.One) ? deviation : deviation * upper);
            result[2][i - start] = middle - (lower.Equals(T.One) ? deviation : deviation * lower);
        }
        return (start, result);
    }

    internal static double?[][] Reference(
        double[] input,
        int p,
        ClassicAverageMethod method,
        double up,
        double down,
        bool first,
        int suppression
    )
    {
        var center = ClassicAverageComparison.ExtendedReference(
            input,
            p,
            method,
            first,
            suppression
        );
        var result = Enumerable.Range(0, 3).Select(_ => new double?[input.Length]).ToArray();
        for (var i = p - 1; i < input.Length; i++)
        {
            if (!center[i].HasValue)
                continue;
            var values = input.Skip(i - p + 1).Take(p).Select(Units).ToArray();
            var n = new BigInteger(p);
            var sum = values.Aggregate(BigInteger.Zero, (a, b) => a + b);
            var squares = values.Aggregate(
                BigInteger.Zero,
                (a, b) =>
                {
                    var delta = n * b - sum;
                    return a + delta * delta;
                }
            );
            var deviation = Units(
                DispersionReferenceArithmetic.Sqrt(squares, n * n * n * Grid * Grid)
            );
            var upper = DirectionalComparison.RoundedUnits(Units(up) * deviation, Grid);
            var lower = DirectionalComparison.RoundedUnits(Units(down) * deviation, Grid);
            result[0][i] = Round(center[i]!.Value + upper, Grid);
            result[1][i] = Round(center[i]!.Value, Grid);
            result[2][i] = Round(center[i]!.Value - lower, Grid);
        }
        return result;
    }
}
