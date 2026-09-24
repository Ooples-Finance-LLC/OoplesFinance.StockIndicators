using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? MamaFormulas(IBuiltInIndicator indicator)
    {
        if (indicator.BatchName != IndicatorName.EhlersMotherOfAdaptiveMovingAverages) return null;
        var options = indicator.CreateOptions();
        return new("Mama", new[] { "Fama", "Mama", "I1", "Q1", "SmoothPeriod", "Smooth", "Real", "Imag" },
            bars => MamaReference(Closes(bars), Number(options, .5, "FastLimit"), Number(options, .05, "SlowLimit")));
    }

    private static IReadOnlyDictionary<string, double[]> MamaReference(double[] prices, double fast = .5, double slow = .05)
    {
        var n = prices.Length;
        var smooth = prices.Select((_, i) => Enumerable.Range(0, Math.Min(4, i + 1)).Sum(lag => (4 - lag) * prices[i - lag]) / 10).ToArray();
        var detrended = new double[n]; var inPhase = new double[n]; var quadrature = new double[n];
        var periods = new double[n]; var real = new double[n]; var imaginary = new double[n];
        var gains = new double[n]; var phase = new double[n];
        Complex previousPhasor = Complex.Zero, covariance = Complex.Zero;
        double period = 0;
        double Fir(double[] values, int i) => new[] { (0, .0962), (2, .5769), (4, -.5769), (6, -.0962) }
            .Where(tap => tap.Item1 <= i).Sum(tap => tap.Item2 * values[i - tap.Item1]);
        for (var i = 0; i < n; i++)
        {
            var correction = .54 + .075 * period;
            detrended[i] = correction * Fir(smooth, i);
            inPhase[i] = i < 3 ? 0 : detrended[i - 3];
            quadrature[i] = correction * Fir(detrended, i);
            var analytic = new Complex(inPhase[i] - correction * Fir(quadrature, i), quadrature[i] + correction * Fir(inPhase, i));
            var phasor = .2 * analytic + .8 * previousPhasor;
            covariance = .2 * Complex.Conjugate(phasor) * previousPhasor + .8 * covariance;
            real[i] = covariance.Real; imaginary[i] = covariance.Imaginary;
            var advance = covariance.Real == 0 ? 0 : Math.Atan(covariance.Imaginary / covariance.Real);
            var measured = advance == 0 ? 0 : 2 * Math.PI / advance;
            if (period != 0) measured = Clamp(measured, .67 * period, 1.5 * period);
            period = .2 * Clamp(measured, 6, 50) + .8 * period;
            periods[i] = period;
            phase[i] = inPhase[i] == 0 ? 0 : Math.Atan(quadrature[i] / inPhase[i]) * 180 / Math.PI;
            gains[i] = Math.Max(slow, fast / Math.Max(1, (i == 0 ? 0 : phase[i - 1]) - phase[i]));
            previousPhasor = phasor;
        }
        // Explicit observation weights for both variable-gain averages, zero initial state.
        double[] WeightedHistory(double[] values, double[] weights) => values.Select((_, i) =>
        {
            double retained = 1, total = 0;
            for (var j = i; j >= 0; j--) { total += retained * weights[j] * values[j]; retained *= 1 - weights[j]; }
            return total;
        }).ToArray();
        var mama = WeightedHistory(prices, gains);
        var fama = WeightedHistory(mama, gains.Select(g => g / 2).ToArray());
        var smoothedPeriod = WeightedHistory(periods, Enumerable.Repeat(.33, n).ToArray());
        return Outputs(("Fama", fama), ("Mama", mama), ("I1", inPhase), ("Q1", quadrature),
            ("SmoothPeriod", smoothedPeriod), ("Smooth", smooth), ("Real", real), ("Imag", imaginary));
    }
}
