using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static bool HasSimpleVolumeMean(IBuiltInIndicator indicator) =>
        indicator.BatchName == IndicatorName.VolumeWeightedMovingAverage && AverageKind(indicator.CreateOptions(), 1) == 1;

    internal static double[] RoundedRollingVolumeMean(IReadOnlyList<Bar> bars, int length)
    {
        var result = new double[bars.Count];
        for (var i = length - 1; i < bars.Count; i++)
        {
            var numerator = new ReferenceFraction(0); var denominator = new ReferenceFraction(0);
            for (var j = i - length + 1; j <= i; j++)
            {
                var weight = ReferenceFraction.FromDouble(bars[j].Volume);
                numerator += ReferenceFraction.FromDouble(bars[j].Close) * weight;
                denominator += weight;
            }
            result[i] = denominator.Sign == 0 ? 0 : (numerator / denominator).ToDouble();
        }
        return result;
    }

    internal static double[] RoundedVolumeWeightedPrice(IReadOnlyList<Bar> bars)
    {
        var dollars = new ReferenceFraction(0); var volume = new ReferenceFraction(0);
        var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var b = bars[i];
            var typical = ReferenceFraction.FromDouble(ExactPriceMean(b.High, b.Low, b.Close));
            var weight = ReferenceFraction.FromDouble(b.Volume);
            dollars += typical * weight; volume += weight;
            result[i] = volume.Sign == 0 ? 0 : (dollars / volume).ToDouble();
        }
        return result;
    }

    internal static double[] RoundedWindowedVolumeMean(IReadOnlyList<Bar> bars, int length)
    {
        var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var numerator = new ReferenceFraction(0); var denominator = new ReferenceFraction(0);
            for (var j = Math.Max(0, i - length + 1); j <= i; j++)
            {
                // Independent normalized Bartlett shape, not the production integer taper.
                var taper = length == 1 ? new ReferenceFraction(1)
                    : new ReferenceFraction(1) - (new ReferenceFraction(2L * (i - j)) / new ReferenceFraction(length) - new ReferenceFraction(1)).Abs();
                var weight = taper * ReferenceFraction.FromDouble(bars[j].Volume);
                numerator += weight * ReferenceFraction.FromDouble(bars[j].Close);
                denominator += weight;
            }
            result[i] = denominator.Sign == 0 ? 0 : (numerator / denominator).ToDouble();
        }
        return result;
    }
}
