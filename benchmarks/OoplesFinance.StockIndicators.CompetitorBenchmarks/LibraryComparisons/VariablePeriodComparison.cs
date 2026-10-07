using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class VariablePeriodComparison
{
    internal static double Selection(Bar bar) => 1 + Math.Abs(bar.Close % 11);

    internal static ComparisonPair Pair(
        int minimum = 2,
        int maximum = 30,
        ClassicAverageMethod method = ClassicAverageMethod.Sma,
        bool first = false,
        int suppression = 0
    ) =>
        new(
            "TaLib.Functions.Mavp",
            nameof(VariablePeriodClassicAverage),
            (d, _) => Native(d, minimum, maximum, method),
            (d, _) => Owned(d.IndicatorBars, minimum, maximum, method, first, suppression),
            (d, _) =>
                VolumePriceComparison.Mask(
                    Reference(
                        d.Closes,
                        d.IndicatorBars.Select(Selection).ToArray(),
                        minimum,
                        maximum,
                        method,
                        first,
                        suppression
                    )
                ),
            MinimumInputCount: (int)
                Math.Max(2, ClassicAverageComparison.First(maximum, method, suppression)),
            CompetitorReference: (d, _) =>
            {
                var packed = NativePacked(
                    d.Closes,
                    d.IndicatorBars.Select(Selection).ToArray(),
                    minimum,
                    maximum,
                    method,
                    first,
                    suppression,
                    0,
                    d.Count - 1
                );
                var values = new double?[d.Count];
                for (var i = 0; i < packed.Values.Length; i++)
                    values[packed.Start + i] = packed.Values[i];
                return VolumePriceComparison.Mask(values);
            },
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Owned(
        Bar[] bars,
        int minimum,
        int maximum,
        ClassicAverageMethod method,
        bool first,
        int suppression,
        Func<Bar, double>? selector = null
    )
    {
        var indicator = new VariablePeriodClassicAverage(
            selector ?? Selection,
            minimum,
            maximum,
            method,
            first,
            suppression
        );
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = run[indicator.Average].ToArray();
        var masks = run[indicator.IsDefined].ToArray();
        return VolumePriceComparison.Mask(
            values.Select((v, i) => masks[i] > 0 ? (double?)v : null).ToArray()
        );
    }

    private static ComparisonSeries Native(
        CompetitorData data,
        int minimum,
        int maximum,
        ClassicAverageMethod method
    )
    {
        var output = new double[data.Count];
        var code = Functions.Mavp<double>(
            data.Closes,
            data.IndicatorBars.Select(Selection).ToArray(),
            System.Range.All,
            output,
            out var range,
            minimum,
            maximum,
            (TaCore.MAType)method
        );
        if (code != TaCore.RetCode.Success)
            throw new InvalidOperationException("TA MAVP: " + code);
        var values = new double?[data.Count];
        for (var i = range.Start.Value; i < range.End.Value; i++)
            values[i] = output[i - range.Start.Value];
        return VolumePriceComparison.Mask(values);
    }

    internal static double?[] Reference(
        double[] prices,
        double[] selections,
        int minimum,
        int maximum,
        ClassicAverageMethod method,
        bool first,
        int suppression
    )
    {
        var start = ClassicAverageComparison.First(maximum, method, suppression);
        var values = new double?[prices.Length];
        var alternatives = new Dictionary<int, BigInteger?[]>();
        for (var i = (int)Math.Min(prices.Length, start); i < prices.Length; i++)
        {
            var period = (int)Math.Clamp(Math.Truncate(selections[i]), minimum, maximum);
            if (!alternatives.TryGetValue(period, out var average))
            {
                average = ClassicAverageComparison.AlignedExtendedReference(
                    prices,
                    period,
                    method,
                    first,
                    suppression,
                    start
                );
                alternatives.Add(period, average);
            }
            values[i] = average[i].HasValue ? Round(average[i]!.Value, Grid) : null;
        }
        return values;
    }

    internal static (int Start, T[] Values) NativePacked<T>(
        T[] prices,
        T[] selections,
        int minimum,
        int maximum,
        ClassicAverageMethod method,
        bool first,
        int suppression,
        int requested,
        int end
    )
        where T : IFloatingPointIeee754<T>
    {
        var start = (int)
            Math.Max(requested, ClassicAverageComparison.First(maximum, method, suppression));
        if (start > end)
            return (0, []);
        var output = new T[end - start + 1];
        var periods = selections
            .Skip(start)
            .Take(output.Length)
            .Select(v => Math.Clamp(int.CreateTruncating(v), minimum, maximum))
            .ToArray();
        foreach (var period in periods.Distinct())
        {
            var candidate = ClassicNativeReference.Packed(
                prices,
                period,
                method,
                first,
                suppression,
                start,
                end
            );
            for (var i = 0; i < output.Length; i++)
                if (periods[i] == period)
                    output[i] = candidate[i];
        }
        return (start, output);
    }
}
