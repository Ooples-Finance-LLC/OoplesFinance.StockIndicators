using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ClassicOscillatorComparison
{
    internal static readonly ComparisonPair[] Pairs = [Pair(false), Pair(true)];

    internal static ComparisonPair Pair(
        bool percent,
        int fast = 12,
        int slow = 26,
        ClassicAverageMethod method = ClassicAverageMethod.Sma,
        bool first = false,
        int suppression = 0
    ) =>
        new(
            percent ? "TaLib.Functions.Ppo" : "TaLib.Functions.Apo",
            nameof(ClassicPriceOscillator),
            (d, _) => Native(d, percent, fast, slow, method),
            (d, _) => Owned(d.IndicatorBars, percent, fast, slow, method, first, suppression),
            (d, _) =>
                VolumePriceComparison.Mask(
                    Reference(d.Closes, percent, fast, slow, method, first, suppression)
                ),
            MinimumInputCount: 2,
            CompetitorReference: (d, _) =>
                NativeReference(d.Closes, percent, fast, slow, method, first, suppression),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Owned(
        Bar[] bars,
        bool percent,
        int fast,
        int slow,
        ClassicAverageMethod method,
        bool first,
        int suppression
    )
    {
        var indicator = new ClassicPriceOscillator(fast, slow, method, percent, first, suppression);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = run[indicator.Value].ToArray();
        var mask = run[indicator.IsDefined].ToArray();
        return VolumePriceComparison.Mask(
            values.Select((v, i) => mask[i] > 0 ? (double?)v : null).ToArray()
        );
    }

    private static ComparisonSeries Native(
        CompetitorData d,
        bool percent,
        int fast,
        int slow,
        ClassicAverageMethod method
    )
    {
        var values = new double?[d.Count];
        if (d.Count == 0)
            return VolumePriceComparison.Mask(values);
        var output = new double[d.Count];
        System.Range range;
        var code = percent
            ? Functions.Ppo<double>(
                d.Closes,
                System.Range.All,
                output,
                out range,
                fast,
                slow,
                (TaCore.MAType)method
            )
            : Functions.Apo<double>(
                d.Closes,
                System.Range.All,
                output,
                out range,
                fast,
                slow,
                (TaCore.MAType)method
            );
        if (code != TaCore.RetCode.Success)
            throw new InvalidOperationException("TA price oscillator: " + code);
        for (var i = range.Start.Value; i < range.End.Value; i++)
            values[i] = output[i - range.Start.Value];
        return VolumePriceComparison.Mask(values);
    }

    private static ComparisonSeries NativeReference(
        double[] input,
        bool percent,
        int fast,
        int slow,
        ClassicAverageMethod method,
        bool first,
        int suppression
    )
    {
        var result = new double?[input.Length];
        var packed = NativePacked(
            input,
            percent,
            fast,
            slow,
            method,
            first,
            suppression,
            0,
            input.Length - 1
        );
        var start = ClassicAverageComparison.First(Math.Max(fast, slow), method, suppression);
        for (var i = start; i < input.Length; i++)
            result[(int)i] = packed[(int)(i - start)];
        return VolumePriceComparison.Mask(result);
    }

    internal static T[] NativePacked<T>(
        T[] input,
        bool percent,
        int fast,
        int slow,
        ClassicAverageMethod method,
        bool first,
        int suppression,
        int requested,
        int end
    )
        where T : IFloatingPointIeee754<T>
    {
        var shortPeriod = Math.Min(fast, slow);
        var longPeriod = Math.Max(fast, slow);
        var f = ClassicNativeReference.Packed(
            input,
            shortPeriod,
            method,
            first,
            suppression,
            requested,
            end
        );
        var s = ClassicNativeReference.Packed(
            input,
            longPeriod,
            method,
            first,
            suppression,
            requested,
            end
        );
        var difference = (int)(
            Math.Max(requested, ClassicAverageComparison.First(longPeriod, method, suppression))
            - Math.Max(requested, ClassicAverageComparison.First(shortPeriod, method, suppression))
        );
        return s.Select(
                (v, i) =>
                    percent
                        ? T.IsZero(v)
                            ? T.Zero
                            : (f[i + difference] - v) / v * T.CreateChecked(100)
                        : f[i + difference] - v
            )
            .ToArray();
    }

    internal static double?[] Reference(
        double[] input,
        bool percent,
        int fast,
        int slow,
        ClassicAverageMethod method,
        bool first,
        int suppression
    )
    {
        var f = ClassicAverageComparison.ExtendedReference(
            input,
            Math.Min(fast, slow),
            method,
            first,
            suppression
        );
        var s = ClassicAverageComparison.ExtendedReference(
            input,
            Math.Max(fast, slow),
            method,
            first,
            suppression
        );
        var result = new double?[input.Length];
        for (var i = 0; i < input.Length; i++)
        {
            if (!f[i].HasValue || !s[i].HasValue)
                continue;
            var fu = f[i]!.Value;
            var su = s[i]!.Value;
            result[i] = percent
                ? su.IsZero
                    ? 0
                    : Round(100 * (fu - su), su)
                : Round(fu - su, Grid);
        }
        return result;
    }
}
