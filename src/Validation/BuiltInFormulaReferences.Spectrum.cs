using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    // Direct channel trajectories, rather than the production per-bar mutable filter bank.
    internal static double[] SpectrumCycles(IReadOnlyList<Bar> bars, int minimum, int maximum, int cutoff, int medianLength)
    {
        minimum = Math.Max(3, minimum);
        maximum = Math.Max(minimum, maximum);
        cutoff = Math.Max(3, cutoff);
        medianLength = Math.Max(1, medianLength);
        var highPass = new double[bars.Count];
        var smooth = new double[bars.Count];
        var angle = 2 * Math.PI / cutoff;
        var highPassPole = (1 - Math.Sin(angle)) / Math.Cos(angle);
        var taps = new[] { 1d, 2, 3, 3, 2, 1 };
        for (var i = 0; i < bars.Count; i++)
        {
            var change = bars[i].Close - (i == 0 ? 0 : bars[i - 1].Close);
            highPass[i] = i < 7 ? bars[i].Close : highPassPole * highPass[i - 1] + (1 + highPassPole) * change / 2;
            smooth[i] = i < 7 ? change : taps.Select((weight, lag) => weight * highPass[i - lag]).Sum() / 12;
        }
        double Smoothed(int i) => i < 0 ? 0 : smooth[i];
        var powers = new double[maximum - minimum + 1][];
        for (var period = minimum; period <= maximum; period++)
        {
            var trajectory = new Complex[bars.Count];
            var power = new double[bars.Count];
            var phase = 2 * Math.PI / period;
            Complex Input(int i) => i < 0 ? Complex.Zero : new Complex(smooth[i], (Smoothed(i) - Smoothed(i - 1)) / phase);
            for (var i = 0; i < bars.Count; i++)
            {
                var bandwidth = Math.Max(.15, .5 - .015 * i);
                var secant = 1 / Math.Cos(2 * phase * bandwidth);
                var pole = 1 / (secant + Math.Sign(secant) * Math.Sqrt(Math.Max(0, secant * secant - 1)));
                var previous = i == 0 ? Complex.Zero : trajectory[i - 1];
                var prior = i < 2 ? Complex.Zero : trajectory[i - 2];
                trajectory[i] = (1 - pole) / 2 * (Input(i) - Input(i - 2))
                    + Math.Cos(phase) * (1 + pole) * previous - pole * prior;
                power[i] = trajectory[i].Real * trajectory[i].Real + trajectory[i].Imaginary * trajectory[i].Imaginary;
            }
            powers[period - minimum] = power;
        }
        var cycles = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var peak = powers.Max(p => p[i]);
            var weights = powers.Select(p =>
            {
                if (peak == 0) return 0d;
                var db = 10 * Math.Log(Math.Max(1, 100 - 99 * p[i] / peak)) / Math.Log(10);
                return db <= 3 ? maximum - db : 0;
            }).ToArray();
            var mass = weights.Sum();
            cycles[i] = mass == 0 ? minimum : weights.Select((weight, index) => weight * (minimum + index)).Sum() / mass;
        }
        return cycles.Select((_, i) =>
        {
            var window = Window(cycles, i, medianLength).OrderBy(v => v).ToArray();
            return (window[(window.Length - 1) / 2] + window[window.Length / 2]) / 2;
        }).ToArray();
    }
}
