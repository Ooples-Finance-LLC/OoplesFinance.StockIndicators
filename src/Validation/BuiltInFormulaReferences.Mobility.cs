using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Builder.Specs;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? MobilityFormula(IBuiltInIndicator indicator)
    {
        if (indicator.CreateOptions() is not MobilityOscillatorSpecOptions options) return null;
        if (AverageKind(options, 2) == 0) return null;
        return new("Mo", new[] { "Mo", "Signal" }, bars => MobilityValues(bars, options.Length, options.MaType));
    }

    internal static Dictionary<string, double[]> MobilityValues(IReadOnlyList<Bar> bars, int length, MovingAvgType kind)
    {
        length = Math.Max(1, length);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var zero = R(0); var one = R(1); var raw = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            raw[i] = zero;
            if (i < length) continue;
            var sample = bars.Skip(i - length + 1).Take(length).ToArray();
            var lower = R(sample.Min(b => b.Low)); var upper = R(sample.Max(b => b.High));
            if (lower.CompareTo(upper) == 0) continue;
            var width = (upper - lower) / R(10);
            var masses = new ReferenceFraction[10]; var mode = 0; var priceMass = zero;
            var price = R(bars[i - length].Close);
            for (var bin = 0; bin < 10; bin++)
            {
                var left = lower + width * R(bin); var right = left + width; var mass = zero;
                foreach (var bar in sample)
                {
                    var low = R(bar.Low); var high = R(bar.High);
                    if (low.CompareTo(high) == 0)
                    {
                        if (low.CompareTo(left) >= 0 && (low.CompareTo(right) < 0 || bin == 9)) mass += one;
                    }
                    else
                    {
                        // Independently integrate each uniform candle distribution.
                        ReferenceFraction Cdf(ReferenceFraction edge) => edge.CompareTo(low) <= 0 ? zero
                            : edge.CompareTo(high) >= 0 ? one : (edge - low) / (high - low);
                        mass += Cdf(right) - Cdf(left);
                    }
                }
                masses[bin] = mass;
                if (mass.CompareTo(masses[mode]) > 0) mode = bin;
                if (price.CompareTo(left) >= 0 && (price.CompareTo(right) < 0 || bin == 9 && price.CompareTo(right) == 0)) priceMass = mass;
            }
            if (masses[mode].Sign == 0) continue;
            var center = lower + (R(mode) + one / R(2)) * width;
            raw[i] = R(price.CompareTo(center) < 0 ? 100 : -100) * (one - priceMass / masses[mode]);
        }
        var line = RationalAverage(raw, 7, kind); var signal = RationalAverage(line, 7, kind);
        return new() { ["Mo"] = line.Select(v => v.ToDouble()).ToArray(), ["Signal"] = signal.Select(v => v.ToDouble()).ToArray() };
    }
}
