using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? SineWaveFormula(IBuiltInIndicator indicator)
    {
        if (indicator.BatchName is not (IndicatorName.EhlersSineWaveIndicatorV1 or IndicatorName.EhlersSineWaveIndicatorV2)) return null;
        var first = indicator.BatchName == IndicatorName.EhlersSineWaveIndicatorV1;
        var options = indicator.CreateOptions();
        return new("Sine", new[] { "Sine", "LeadSine" }, bars =>
        {
            var prices = Closes(bars);
            var mama = first ? MamaReference(prices) : null;
            var periods = first ? mama!["SmoothPeriod"] : AdaptiveCyberPeriods(prices, Integer(options, "Length"), Number(options, .07, "Alpha"));
            var values = first ? mama!["Smooth"] : CyberCycleReference(prices, .07);
            var sine = new double[bars.Count]; var lead = new double[bars.Count];
            for (var i = 0; i < bars.Count; i++)
            {
                var nearest = Math.Round(periods[i]);
                var period = first ? (int)Math.Ceiling(periods[i]+.5)
                    : (int)(Math.Abs(periods[i]-nearest) <= 1e-9*Math.Max(1, Math.Abs(periods[i])) ? nearest : Math.Ceiling(periods[i]));
                var phasor = Enumerable.Range(0, Math.Min(i+1, period))
                    .Aggregate(Complex.Zero, (sum, lag) => sum+values[i-lag]*Complex.FromPolarCoordinates(1, 2*Math.PI*lag/period));
                var resolution = 64*Math.Pow(2, -52)*Enumerable.Range(0, Math.Min(i+1, period)).Sum(lag => Math.Abs(values[i-lag]));
                phasor = new Complex(Math.Abs(phasor.Real) <= resolution ? 0 : phasor.Real,
                    Math.Abs(phasor.Imaginary) <= resolution ? 0 : phasor.Imaginary);
                var phase = Math.Abs(phasor.Real) > .001 ? Math.Atan2(phasor.Imaginary, phasor.Real)
                    : Math.PI/2*Math.Sign(phasor.Imaginary)+(phasor.Real < 0 ? Math.PI : 0);
                phase += Math.PI/2 + (first && periods[i] != 0 ? 2*Math.PI/periods[i] : 0);
                sine[i] = Math.Sin(phase); lead[i] = Math.Sin(phase+Math.PI/4);
            }
            return Outputs(("Sine", sine), ("LeadSine", lead));
        });
    }
}
