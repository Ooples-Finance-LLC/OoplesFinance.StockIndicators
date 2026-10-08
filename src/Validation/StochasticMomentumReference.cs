namespace OoplesFinance.StockIndicators.Validation;

internal static class StochasticMomentumReference
{
    private static (ReferenceFraction Delta, ReferenceFraction Range) Window(
        IReadOnlyList<Indicators.Bar> bars,
        int period,
        int end
    )
    {
        var high = bars.Skip(end - period + 1).Take(period).Max(b => b.High);
        var low = bars.Skip(end - period + 1).Take(period).Min(b => b.Low);
        var h = ReferenceFraction.FromDouble(high);
        var l = ReferenceFraction.FromDouble(low);
        return (
            (
                ReferenceFraction.FromDouble(bars[end].Close) - (h + l) / new ReferenceFraction(2)
            ).RoundExtendedBinary64(),
            (h - l).RoundExtendedBinary64()
        );
    }

    internal static IReadOnlyList<double> Raw(
        IReadOnlyList<Indicators.Bar> bars,
        int period,
        int slot
    )
    {
        var result = new double[bars.Count];
        for (var i = period - 1; i < result.Length; i++)
            result[i] = slot == 1 ? 1 : Window(bars, period, i).Delta.ToDouble();
        return result;
    }

    internal static IReadOnlyList<double> Index(
        IReadOnlyList<Indicators.Bar> bars,
        int period,
        int first,
        int second,
        int signal,
        int slot
    )
    {
        var delta1 = new ReferenceFraction(0);
        var delta2 = delta1;
        var range1 = delta1;
        var range2 = delta1;
        double? previousSignal = null;
        var result = new double[bars.Count];
        ReferenceFraction Smooth(ReferenceFraction value, ReferenceFraction previous, int length) =>
            (
                (value * new ReferenceFraction(2) + previous * new ReferenceFraction(length - 1))
                / new ReferenceFraction((long)length + 1)
            ).RoundExtendedBinary64();
        for (var i = period - 1; i < result.Length; i++)
        {
            var value = Window(bars, period, i);
            if (i == period - 1)
            {
                delta1 = delta2 = value.Delta;
                range1 = range2 = value.Range;
            }
            else
            {
                delta1 = Smooth(value.Delta, delta1, first);
                delta2 = Smooth(delta1, delta2, second);
                range1 = Smooth(value.Range, range1, first);
                range2 = Smooth(range1, range2, second);
            }
            if (range2.Sign == 0)
            {
                previousSignal = null;
                continue;
            }
            var index = (new ReferenceFraction(200) * delta2 / range2).ToDouble();
            if (!FrameworkCompatibility.IsFinite(index))
            {
                result[i] =
                    slot == 0 ? index
                    : slot == 2 ? 1
                    : 0;
                continue;
            }
            previousSignal = previousSignal.HasValue
                ? Smooth(
                        ReferenceFraction.FromDouble(index),
                        ReferenceFraction.FromDouble(previousSignal.Value),
                        signal
                    )
                    .ToDouble()
                : index;
            result[i] =
                slot == 0 ? index
                : slot == 1 ? previousSignal.Value
                : 1;
        }
        return result;
    }
}
