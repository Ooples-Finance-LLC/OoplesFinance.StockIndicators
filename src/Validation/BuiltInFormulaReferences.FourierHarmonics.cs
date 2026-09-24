using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Builder.Specs;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? FourierHarmonicsFormula(IBuiltInIndicator indicator)
    {
        if (indicator.CreateOptions() is not EhlersFourierSeriesAnalysisSpecOptions options) return null;
        return new("Wave", new[] { "Wave", "Roc" }, bars =>
        {
            var prices = Closes(bars); var length = options.Length;
            var harmonics = new List<double[]>(); var energies = new List<double[]>();
            for (var harmonic = 1; harmonic <= 3; harmonic++)
            {
                var period = (double)length/harmonic;
                var frequency = 2*Math.PI/period;
                var dampingAngle = Math.Min(Math.PI/2, Math.Abs(options.Bw)*frequency);
                var damping = period <= 2 ? 1 : Math.Tan(Math.PI/4-dampingAngle/2);
                var feedback = Math.Cos(frequency)*(1+damping);
                var discriminant = Complex.Sqrt(new Complex(feedback*feedback-4*damping, 0));
                var first = (feedback+discriminant)/2; var second = (feedback-discriminant)/2;
                // Expand the transfer function's two poles, then convolve the input
                // difference. This does not share the production filter recurrence.
                var kernel = Enumerable.Range(0, prices.Length).Select(age =>
                    (discriminant.Magnitude < 1e-12 ? (age+1)*Complex.Pow(first, age)
                        : (Complex.Pow(first, age+1)-Complex.Pow(second, age+1))/discriminant).Real).ToArray();
                var line = prices.Select((_, i) => i < 4 || damping == 1 ? 0 : (1-damping)/2
                    * Enumerable.Range(4, i-3).Sum(j => kernel[i-j]*(prices[j]-prices[j-2]))).ToArray();
                var energy = line.Select((v, i) => v*v+(i < 5 ? 0 : Math.Pow((v-line[i-1])/frequency, 2))).ToArray();
                harmonics.Add(line); energies.Add(energy);
            }
            var wave = prices.Select((_, i) =>
            {
                var power = energies.Select(e => Window(e, i, length).Sum()).ToArray();
                return power[0] == 0 ? 0 : Enumerable.Range(0, 3).Sum(h => Math.Sqrt(power[h]/power[0])*harmonics[h][i]);
            }).ToArray();
            return Outputs(("Wave", wave), ("Roc", wave.Select((v, i) => length/(4*Math.PI)*(v-(i < 2 ? 0 : wave[i-2]))).ToArray()));
        });
    }
}
