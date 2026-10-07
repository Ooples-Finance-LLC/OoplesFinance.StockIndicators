using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using TaCore = TALib.Core;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class AlignedMacdComparison
{
    internal static readonly string[] Names = ["Macd", "Signal", "Histogram"];
    internal static readonly ComparisonPair[] Pairs = [Pair(false), Pair(true)];

    internal static ComparisonPair Pair(
        bool fixedCoefficients,
        int fast = 12,
        int slow = 26,
        int signal = 9,
        bool firstPrice = false,
        int suppression = 0
    ) =>
        new(
            fixedCoefficients ? "TaLib.Functions.MacdFix" : "TaLib.Functions.Macd",
            nameof(AlignedEmaMacd),
            (d, p) => Native(d, fast, slow, signal, fixedCoefficients),
            (d, p) =>
                Owned(
                    d.IndicatorBars,
                    fast,
                    slow,
                    signal,
                    firstPrice,
                    suppression,
                    fixedCoefficients
                ),
            (d, p) =>
                Series(
                    Reference(
                        d.Closes,
                        fast,
                        slow,
                        signal,
                        firstPrice,
                        suppression,
                        fixedCoefficients
                    )
                ),
            Names,
            MinimumInputCount: 2,
            CompetitorReference: (d, p) =>
                Series(
                    NativeReference(
                        d.Closes,
                        fast,
                        slow,
                        signal,
                        firstPrice,
                        suppression,
                        fixedCoefficients
                    )
                ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Series(double?[][] rows) =>
        RetrospectivePriceComparison.Series(Names, rows);

    private static ComparisonSeries Native(
        CompetitorData data,
        int fast,
        int slow,
        int signal,
        bool fixedCoefficients
    )
    {
        var rows = new[]
        {
            new double?[data.Count],
            new double?[data.Count],
            new double?[data.Count],
        };
        if (data.Count == 0)
            return Series(rows);
        var buffers = new[]
        {
            new double[data.Count],
            new double[data.Count],
            new double[data.Count],
        };
        System.Range range;
        var code = fixedCoefficients
            ? Functions.MacdFix<double>(
                data.Closes,
                System.Range.All,
                buffers[0],
                buffers[1],
                buffers[2],
                out range,
                signal
            )
            : Functions.Macd<double>(
                data.Closes,
                System.Range.All,
                buffers[0],
                buffers[1],
                buffers[2],
                out range,
                fast,
                slow,
                signal
            );
        if (code != TaCore.RetCode.Success)
            throw new InvalidOperationException("TA MACD failed: " + code);
        for (var j = 0; j < 3; j++)
        for (var i = range.Start.Value; i < range.End.Value; i++)
            rows[j][i] = buffers[j][i - range.Start.Value];
        return Series(rows);
    }

    internal static ComparisonSeries Owned(
        Bar[] bars,
        int fast,
        int slow,
        int signal,
        bool first,
        int suppression,
        bool fixedCoefficients,
        EmaDifferenceSelection selection =
            EmaDifferenceSelection.Oscillator
            | EmaDifferenceSelection.Signal
            | EmaDifferenceSelection.Histogram
    )
    {
        var indicator = new AlignedEmaMacd(
            fast,
            slow,
            signal,
            first,
            suppression,
            fixedCoefficients,
            selection
        );
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
            var present = run[indicator.Outputs[j + 3]].ToArray();
            rows[j] = v.Select((x, i) => present[i] > 0 ? (double?)x : null).ToArray();
        }
        return Series(rows);
    }

    private static BigInteger?[] Average(
        BigInteger?[] input,
        long start,
        int p,
        bool first,
        BigInteger alphaNumerator,
        BigInteger alphaDenominator
    )
    {
        var r = new BigInteger?[input.Length];
        var begin = start + (first ? 0 : p - 1L);
        if (begin >= input.Length)
            return r;
        r[(int)begin] = DirectionalComparison.RoundedUnits(
            input
                .Skip((int)start)
                .Take(first ? 1 : p)
                .Aggregate(BigInteger.Zero, (s, v) => s + v!.Value),
            first ? 1 : p
        );
        for (var i = (int)begin + 1; i < input.Length; i++)
            r[i] = DirectionalComparison.RoundedUnits(
                r[i - 1]!.Value * (alphaDenominator - alphaNumerator)
                    + input[i]!.Value * alphaNumerator,
                alphaDenominator
            );
        return r;
    }

    internal static double?[][] Reference(
        double[] prices,
        int fast,
        int slow,
        int signal,
        bool first,
        int suppression,
        bool fixedCoefficients,
        EmaDifferenceSelection selection =
            EmaDifferenceSelection.Oscillator
            | EmaDifferenceSelection.Signal
            | EmaDifferenceSelection.Histogram
    )
    {
        (fast, slow) = (Math.Min(fast, slow), Math.Max(fast, slow));
        var input = prices.Select(v => (BigInteger?)Units(v)).ToArray();
        var f = Average(
            input,
            first ? 0 : slow - fast,
            fast,
            first,
            fixedCoefficients ? Units(.15) : 2,
            fixedCoefficients ? Grid : (long)fast + 1
        );
        var s = Average(
            input,
            0,
            slow,
            first,
            fixedCoefficients ? Units(.075) : 2,
            fixedCoefficients ? Grid : (long)slow + 1
        );
        var start = (long)slow - 1 + suppression;
        var difference = new BigInteger?[prices.Length];
        for (var i = start; i < prices.Length; i++)
            difference[(int)i] = DirectionalComparison.RoundedUnits(
                f[(int)i]!.Value - s[(int)i]!.Value,
                1
            );
        var sig = Average(difference, start, signal, first, 2, (long)signal + 1);
        var begin = start + signal - 1L + suppression;
        var rows = new[]
        {
            new double?[prices.Length],
            new double?[prices.Length],
            new double?[prices.Length],
        };
        for (var i = begin; i < prices.Length; i++)
        {
            var values = new[]
            {
                difference[(int)i]!.Value,
                sig[(int)i]!.Value,
                DirectionalComparison.RoundedUnits(
                    difference[(int)i]!.Value - sig[(int)i]!.Value,
                    1
                ),
            };
            for (var j = 0; j < 3; j++)
                if (((int)selection & (1 << j)) != 0)
                    rows[j][(int)i] = Round(values[j], Grid);
        }
        return rows;
    }

    internal static double?[][] NativeReference(
        double[] prices,
        int fast,
        int slow,
        int signal,
        bool first,
        int suppression,
        bool fixedCoefficients
    )
    {
        var rows = new[]
        {
            new double?[prices.Length],
            new double?[prices.Length],
            new double?[prices.Length],
        };
        var start = (long)Math.Max(fast, slow) + signal - 2 + 2L * suppression;
        if (start >= prices.Length)
            return rows;
        var packed = NativePacked(
            prices,
            fast,
            slow,
            signal,
            first,
            suppression,
            fixedCoefficients,
            0,
            prices.Length - 1
        );
        for (var j = 0; j < 3; j++)
        for (var i = 0; i < packed[j].Length; i++)
            rows[j][(int)start + i] = packed[j][i];
        return rows;
    }

    // Independent stage arrays preserve per-request seed alignment and native
    // arithmetic in either float or double, including truncated signal histories.
    internal static T[][] NativePacked<T>(
        T[] prices,
        int fast,
        int slow,
        int signal,
        bool first,
        int suppression,
        bool fixedCoefficients,
        int requestedStart,
        int end
    )
        where T : IFloatingPointIeee754<T>
    {
        (fast, slow) = (Math.Min(fast, slow), Math.Max(fast, slow));
        var signalLookback = signal - 1 + suppression;
        var start = Math.Max(requestedStart, slow - 1 + suppression + signalLookback);
        if (start > end)
            return
            [
                [],
                [],
                [],
            ];
        T[] Ema(T[] values, int p, int requested, T alpha)
        {
            var lookback = p - 1 + suppression;
            var begin = Math.Max(requested, lookback);
            if (begin >= values.Length)
                return [];
            var state = T.Zero;
            var index = first ? 1 : begin - lookback + p;
            if (first)
                state = values[0];
            else
            {
                for (var i = begin - lookback; i < index; i++)
                    state += values[i];
                state /= T.CreateChecked(p);
            }
            for (; index <= begin; index++)
                state = (values[index] - state) * alpha + state;
            var r = new T[values.Length - begin];
            r[0] = state;
            for (var i = begin + 1; i < values.Length; i++)
            {
                state = (values[i] - state) * alpha + state;
                r[i - begin] = state;
            }
            return r;
        }
        var rawStart = start - signalLookback;
        var input = prices.Take(end + 1).ToArray();
        var f = Ema(
            input,
            fast,
            rawStart,
            fixedCoefficients
                ? T.CreateChecked(.15)
                : T.CreateChecked(2) / (T.CreateChecked(fast) + T.One)
        );
        var s = Ema(
            input,
            slow,
            rawStart,
            fixedCoefficients
                ? T.CreateChecked(.075)
                : T.CreateChecked(2) / (T.CreateChecked(slow) + T.One)
        );
        var difference = f.Select((v, i) => v - s[i]).ToArray();
        var sig = Ema(
            difference,
            signal,
            0,
            T.CreateChecked(2) / (T.CreateChecked(signal) + T.One)
        );
        var output = difference.Skip(signalLookback).ToArray();
        return [output, sig, output.Select((v, i) => v - sig[i]).ToArray()];
    }

    internal static TaCore.RetCode Invoke<T>(
        T[] input,
        System.Range range,
        T[][] output,
        out System.Range outputRange,
        int fast,
        int slow,
        int signal,
        bool fixedCoefficients
    )
        where T : IFloatingPointIeee754<T> =>
        fixedCoefficients
            ? Functions.MacdFix<T>(
                input,
                range,
                output[0],
                output[1],
                output[2],
                out outputRange,
                signal
            )
            : Functions.Macd<T>(
                input,
                range,
                output[0],
                output[1],
                output[2],
                out outputRange,
                fast,
                slow,
                signal
            );
}
