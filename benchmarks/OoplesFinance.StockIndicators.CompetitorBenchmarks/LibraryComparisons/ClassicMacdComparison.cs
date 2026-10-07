using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ClassicMacdComparison
{
    internal static ComparisonPair Pair(
        int fast = 12,
        int slow = 26,
        int signal = 9,
        ClassicAverageMethod fastMethod = ClassicAverageMethod.Sma,
        ClassicAverageMethod slowMethod = ClassicAverageMethod.Sma,
        ClassicAverageMethod signalMethod = ClassicAverageMethod.Sma,
        bool first = false,
        int suppression = 0
    ) =>
        new(
            "TaLib.Functions.MacdExt",
            nameof(ClassicMacd),
            (d, _) => Native(d, fast, slow, signal, fastMethod, slowMethod, signalMethod),
            (d, _) =>
                Owned(
                    d.IndicatorBars,
                    fast,
                    slow,
                    signal,
                    fastMethod,
                    slowMethod,
                    signalMethod,
                    first,
                    suppression
                ),
            (d, _) =>
                AlignedMacdComparison.Series(
                    Reference(
                        d.Closes,
                        fast,
                        slow,
                        signal,
                        fastMethod,
                        slowMethod,
                        signalMethod,
                        first,
                        suppression
                    )
                ),
            AlignedMacdComparison.Names,
            MinimumInputCount: 2,
            CompetitorReference: (d, _) =>
            {
                var packed = NativePacked(
                    d.Closes,
                    fast,
                    slow,
                    signal,
                    fastMethod,
                    slowMethod,
                    signalMethod,
                    first,
                    suppression,
                    0,
                    d.Count - 1
                );
                var values = Enumerable.Range(0, 3).Select(_ => new double?[d.Count]).ToArray();
                for (var j = 0; j < 3; j++)
                for (var i = 0; i < packed.Values[j].Length; i++)
                    values[j][packed.Start + i] = packed.Values[j][i];
                return AlignedMacdComparison.Series(values);
            },
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Owned(
        Bar[] bars,
        int fast,
        int slow,
        int signal,
        ClassicAverageMethod fm,
        ClassicAverageMethod sm,
        ClassicAverageMethod dm,
        bool first,
        int suppression
    )
    {
        var indicator = new ClassicMacd(fast, slow, signal, fm, sm, dm, first, suppression);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return AlignedMacdComparison.Series(
            Enumerable
                .Range(0, 3)
                .Select(j =>
                {
                    var values = run[indicator.Outputs[j]].ToArray();
                    var flags = run[indicator.Outputs[j + 3]].ToArray();
                    return values.Select((v, i) => flags[i] > 0 ? (double?)v : null).ToArray();
                })
                .ToArray()
        );
    }

    private static ComparisonSeries Native(
        CompetitorData data,
        int fast,
        int slow,
        int signal,
        ClassicAverageMethod fm,
        ClassicAverageMethod sm,
        ClassicAverageMethod dm
    )
    {
        var result = Enumerable.Range(0, 3).Select(_ => new double?[data.Count]).ToArray();
        if (data.Count == 0)
            return AlignedMacdComparison.Series(result);
        var output = Enumerable.Range(0, 3).Select(_ => new double[data.Count]).ToArray();
        var code = Functions.MacdExt<double>(
            data.Closes,
            System.Range.All,
            output[0],
            output[1],
            output[2],
            out var range,
            fast,
            (TaCore.MAType)fm,
            slow,
            (TaCore.MAType)sm,
            signal,
            (TaCore.MAType)dm
        );
        if (code != TaCore.RetCode.Success)
            throw new InvalidOperationException("TA MACDEXT: " + code);
        for (var j = 0; j < 3; j++)
        for (var i = range.Start.Value; i < range.End.Value; i++)
            result[j][i] = output[j][i - range.Start.Value];
        return AlignedMacdComparison.Series(result);
    }

    internal static double?[][] Reference(
        double[] prices,
        int fast,
        int slow,
        int signal,
        ClassicAverageMethod fm,
        ClassicAverageMethod sm,
        ClassicAverageMethod dm,
        bool first,
        int suppression
    )
    {
        if (slow < fast)
        {
            (fast, slow) = (slow, fast);
            (fm, sm) = (sm, fm);
        }
        var start = Math.Max(
            ClassicAverageComparison.First(fast, fm, suppression),
            ClassicAverageComparison.First(slow, sm, suppression)
        );
        var result = Enumerable.Range(0, 3).Select(_ => new double?[prices.Length]).ToArray();
        if (start >= prices.Length)
            return result;
        var a = ClassicAverageComparison.AlignedExtendedReference(
            prices,
            fast,
            fm,
            first,
            suppression,
            start
        );
        var b = ClassicAverageComparison.AlignedExtendedReference(
            prices,
            slow,
            sm,
            first,
            suppression,
            start
        );
        var raw = Enumerable
            .Range((int)start, prices.Length - (int)start)
            .Select(i => DirectionalComparison.RoundedUnits(a[i]!.Value - b[i]!.Value, 1))
            .ToArray();
        var average = WideClassicReference.Values(raw, signal, dm, first, suppression);
        for (var i = 0; i < raw.Length; i++)
        {
            if (!average[i].HasValue)
                continue;
            result[0][(int)start + i] = Round(raw[i], Grid);
            result[1][(int)start + i] = Round(average[i]!.Value, Grid);
            result[2][(int)start + i] = Round(raw[i] - average[i]!.Value, Grid);
        }
        return result;
    }

    internal static (int Start, T[][] Values) NativePacked<T>(
        T[] prices,
        int fast,
        int slow,
        int signal,
        ClassicAverageMethod fm,
        ClassicAverageMethod sm,
        ClassicAverageMethod dm,
        bool first,
        int suppression,
        int requested,
        int end
    )
        where T : IFloatingPointIeee754<T>
    {
        if (slow < fast)
        {
            (fast, slow) = (slow, fast);
            (fm, sm) = (sm, fm);
        }
        var delay = (int)ClassicAverageComparison.First(signal, dm, suppression);
        var firstAverage = (int)
            Math.Max(
                ClassicAverageComparison.First(fast, fm, suppression),
                ClassicAverageComparison.First(slow, sm, suppression)
            );
        var start = Math.Max(requested, firstAverage + delay);
        if (start > end)
            return (
                0,
                [
                    [],
                    [],
                    [],
                ]
            );
        var a = ClassicNativeReference.Packed(
            prices,
            fast,
            fm,
            first,
            suppression,
            start - delay,
            end
        );
        var b = ClassicNativeReference.Packed(
            prices,
            slow,
            sm,
            first,
            suppression,
            start - delay,
            end
        );
        var raw = a.Zip(b, (x, y) => x - y).ToArray();
        if (raw.Length == 1)
            throw new InvalidOperationException("Native signal MA rejects its one-element input.");
        var average = ClassicNativeReference.Packed(
            raw,
            signal,
            dm,
            first,
            suppression,
            0,
            raw.Length - 1
        );
        var oscillator = raw.Skip(delay).Take(average.Length).ToArray();
        return (start, [oscillator, average, oscillator.Zip(average, (x, y) => x - y).ToArray()]);
    }
}
