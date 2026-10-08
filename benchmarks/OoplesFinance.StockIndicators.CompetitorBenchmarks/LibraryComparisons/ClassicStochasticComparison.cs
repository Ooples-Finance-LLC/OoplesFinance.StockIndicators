using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ClassicStochasticComparison
{
    internal static ComparisonPair Pair(
        bool fast = false,
        int window = 5,
        int k = 3,
        int d = 3,
        ClassicAverageMethod km = ClassicAverageMethod.Sma,
        ClassicAverageMethod dm = ClassicAverageMethod.Sma,
        bool first = false,
        int suppression = 0
    ) =>
        new(
            fast ? "TaLib.Functions.StochF" : "TaLib.Functions.Stoch",
            nameof(ClassicStochastic),
            (data, _) => Native(data, fast, window, k, d, km, dm),
            (data, _) =>
                Owned(data.IndicatorBars, window, fast ? 1 : k, d, km, dm, first, suppression),
            (data, _) =>
                Series(
                    Reference(
                        data.IndicatorBars,
                        window,
                        fast ? 1 : k,
                        d,
                        km,
                        dm,
                        first,
                        suppression
                    )
                ),
            ClassicStochasticRsiComparison.Names,
            MinimumInputCount: 2,
            CompetitorReference: (data, _) =>
            {
                var packed = NativePacked(
                    data.Highs,
                    data.Lows,
                    data.Closes,
                    fast,
                    window,
                    k,
                    d,
                    km,
                    dm,
                    first,
                    suppression,
                    0,
                    data.Count - 1
                );
                return Series(Expand(packed, data.Count));
            },
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Series(double?[][] values) =>
        ClassicStochasticRsiComparison.Series(values);

    internal static double?[][] Expand((int Start, double[][] Values) packed, int count)
    {
        var result = Enumerable.Range(0, 2).Select(_ => new double?[count]).ToArray();
        for (var j = 0; j < 2; j++)
        for (var i = 0; i < packed.Values[j].Length; i++)
            result[j][packed.Start + i] = packed.Values[j][i];
        return result;
    }

    internal static ComparisonSeries Owned(
        Bar[] bars,
        int window,
        int k,
        int d,
        ClassicAverageMethod km,
        ClassicAverageMethod dm,
        bool first,
        int suppression
    )
    {
        var owner = new ClassicStochastic(window, k, d, km, dm, first, suppression);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(owner)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return Series(
            Enumerable
                .Range(0, 2)
                .Select(j =>
                {
                    var values = run[owner.Outputs[j]].ToArray();
                    var flags = run[owner.Outputs[j + 2]].ToArray();
                    return values.Select((v, i) => flags[i] > 0 ? (double?)v : null).ToArray();
                })
                .ToArray()
        );
    }

    private static ComparisonSeries Native(
        CompetitorData data,
        bool fast,
        int window,
        int k,
        int d,
        ClassicAverageMethod km,
        ClassicAverageMethod dm
    )
    {
        if (data.Count == 0)
            return Series([
                [],
                [],
            ]);
        var a = new double[data.Count];
        var b = new double[data.Count];
        var code = NativeCall(
            data.Highs,
            data.Lows,
            data.Closes,
            fast,
            System.Range.All,
            a,
            b,
            out var range,
            window,
            k,
            d,
            km,
            dm
        );
        if (code != TaCore.RetCode.Success)
            throw new InvalidOperationException("TA stochastic: " + code);
        var length = range.End.Value - range.Start.Value;
        return Series(
            Expand(
                (range.Start.Value, [a.Take(length).ToArray(), b.Take(length).ToArray()]),
                data.Count
            )
        );
    }

    internal static TaCore.RetCode NativeCall<T>(
        T[] highs,
        T[] lows,
        T[] closes,
        bool fast,
        System.Range range,
        T[] a,
        T[] b,
        out System.Range output,
        int window,
        int k,
        int d,
        ClassicAverageMethod km,
        ClassicAverageMethod dm
    )
        where T : IFloatingPointIeee754<T> =>
        fast
            ? Functions.StochF<T>(
                highs,
                lows,
                closes,
                range,
                a,
                b,
                out output,
                window,
                d,
                (TaCore.MAType)dm
            )
            : Functions.Stoch<T>(
                highs,
                lows,
                closes,
                range,
                a,
                b,
                out output,
                window,
                k,
                (TaCore.MAType)km,
                d,
                (TaCore.MAType)dm
            );

    internal static (int Start, T[][] Values) NativePacked<T>(
        T[] highs,
        T[] lows,
        T[] closes,
        bool fast,
        int window,
        int k,
        int d,
        ClassicAverageMethod km,
        ClassicAverageMethod dm,
        bool first,
        int suppression,
        int start,
        int end,
        bool signalAliasesInput = false
    )
        where T : IFloatingPointIeee754<T> =>
        fast
            ? ClassicStochNativeReference.Fast(
                highs,
                lows,
                closes,
                window,
                d,
                dm,
                first,
                suppression,
                start,
                end,
                signalAliasesInput
            )
            : ClassicStochNativeReference.Slow(
                highs,
                lows,
                closes,
                window,
                k,
                d,
                km,
                dm,
                first,
                suppression,
                start,
                end,
                signalAliasesInput
            );

    internal static double?[][] Reference(
        Bar[] bars,
        int window,
        int k,
        int d,
        ClassicAverageMethod km,
        ClassicAverageMethod dm,
        bool first,
        int suppression
    )
    {
        var output = Enumerable.Range(0, 2).Select(_ => new double?[bars.Length]).ToArray();
        var kd = ClassicAverageComparison.First(k, km, suppression);
        var dd = ClassicAverageComparison.First(d, dm, suppression);
        if (window - 1L + kd + dd >= bars.Length)
            return output;
        var raw = new BigInteger[bars.Length - window + 1];
        for (var i = window - 1; i < bars.Length; i++)
        {
            var high = bars.Skip(i - window + 1).Take(window).Max(b => b.High);
            var low = bars.Skip(i - window + 1).Take(window).Min(b => b.Low);
            var width = Units(high) - Units(low);
            raw[i - window + 1] = width.IsZero
                ? BigInteger.Zero
                : DirectionalComparison.RoundedUnits(
                    100 * (Units(bars[i].Close) - Units(low)) * Grid,
                    width
                );
        }
        var smooth = WideClassicReference.Values(raw, k, km, first, suppression);
        var known = smooth.Skip((int)kd).Select(v => v!.Value).ToArray();
        var signal = WideClassicReference.Values(known, d, dm, first, suppression);
        for (var i = 0; i < signal.Length; i++)
        {
            if (!signal[i].HasValue)
                continue;
            var offset = window - 1 + (int)kd + i;
            output[0][offset] = Round(known[i], Grid);
            output[1][offset] = Round(signal[i]!.Value, Grid);
        }
        return output;
    }
}
