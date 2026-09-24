using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? Autocorrelation(IBuiltInIndicator indicator)
    {
        var name = indicator.BatchName;
        if (name is not (IndicatorName.EhlersAutoCorrelationIndicator or IndicatorName.EhlersAutoCorrelationPeriodogram or IndicatorName.EhlersAutoCorrelationReversals)) return null;
        var options = indicator.CreateOptions();
        var upper = Integer(options, "Length1", 48);
        var lower = Integer(options, "Length2", 10);
        var firstLag = Integer(options, "Length3", 3);
        var key = name == IndicatorName.EhlersAutoCorrelationIndicator ? "Eaci" : name == IndicatorName.EhlersAutoCorrelationReversals ? "Eacr" : "Eacp";
        return new(key, new[] { key }, bars =>
        {
            var correlation = AutocorrelationTrajectory(HilbertRoofingTrajectory(Closes(bars), upper, lower), upper);
            if (name == IndicatorName.EhlersAutoCorrelationReversals)
            {
                var crossings = correlation.Select((v, i) => i == 0 ? 0 :
                    (v > .5 && correlation[i - 1] < .5) || (v < .5 && correlation[i - 1] > .5) ? 1 : 0).ToArray();
                return Outputs((key, correlation.Select((_, i) => Enumerable.Range(firstLag, Math.Max(0, upper - firstLag + 1))
                    .Sum(lag => i - lag + 1 < 0 ? 0 : crossings[i - lag + 1]) > upper / 2d ? 1d : 0d).ToArray()));
            }
            return Outputs((key, name == IndicatorName.EhlersAutoCorrelationIndicator ? correlation
                : AutocorrelationSpectrum(correlation, upper, lower, firstLag)));
        });
    }

    internal static double[] AutocorrelationTrajectory(double[] values, int period) => values.Select((_, i) =>
    {
        var indices = Enumerable.Range(Math.Max(0, i - period + 1), Math.Min(period, i + 1)).ToArray();
        var x = indices.Select(j => values[j]).ToArray();
        var y = indices.Select(j => j < period ? 0 : values[j - period]).ToArray();
        var mx = x.Average();
        var my = y.Average();
        var xx = x.Select(v => (v - mx) * (v - mx)).Sum();
        var yy = y.Select(v => (v - my) * (v - my)).Sum();
        if (xx == 0 || yy == 0) return 0d;
        var covariance = x.Zip(y, (a, b) => (a - mx) * (b - my)).Sum();
        return .5 * (1 + covariance / (Math.Sqrt(xx) * Math.Sqrt(yy)));
    }).ToArray();

    private static double[] AutocorrelationSpectrum(double[] correlation, int upper, int lower, int firstLag)
    {
        var periods = Enumerable.Range(lower, Math.Max(0, upper - lower + 1)).ToArray();
        var decay = Enumerable.Range(0, correlation.Length).Select(i => .2 * Math.Pow(.8, i)).ToArray();
        var spectra = periods.Select(period =>
        {
            var kernels = Enumerable.Range(firstLag, Math.Max(0, upper - firstLag + 1))
                .Select(lag => (Lag: lag, Phase: Complex.FromPolarCoordinates(1, 2 * Math.PI * lag / period))).ToArray();
            var energy = correlation.Select((_, i) =>
            {
                var vector = kernels.Where(k => i >= k.Lag).Select(k => correlation[i - k.Lag] * k.Phase)
                    .Aggregate(Complex.Zero, (sum, value) => sum + value);
                var square = vector.Real * vector.Real + vector.Imaginary * vector.Imaginary;
                return square * square;
            }).ToArray();
            return energy.Select((_, i) => Enumerable.Range(0, i + 1).Sum(j => energy[j] * decay[i - j])).ToArray();
        }).ToArray();
        return correlation.Select((_, i) =>
        {
            var peak = spectra.Select(s => s[i]).DefaultIfEmpty(0).Max();
            if (peak == 0) return 0d;
            var selected = Enumerable.Range(0, periods.Length).Where(j => spectra[j][i] >= peak / 2).ToArray();
            return selected.Sum(j => periods[j] * (spectra[j][i] / peak)) / selected.Sum(j => spectra[j][i] / peak);
        }).ToArray();
    }
}
