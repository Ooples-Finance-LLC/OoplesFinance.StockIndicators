using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) FourierPhaseValues(IReadOnlyList<Bar> bars, int length, int kind = 3, IReadOnlyList<double>? externalAverage = null)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        length = Math.Max(2, length); var phases = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var baseline = i + 1L >= length ? R(bars[i].Close) : R(0); var real = R(0); var imaginary = R(0); var mass = R(0);
            for (var lag = 0; lag < length && lag <= i; lag++)
            {
                var weight = R(bars[i - lag].Close) - baseline; var angle = 2 * Math.PI * lag / length;
                real += weight * R(Math.Cos(angle)); imaginary += weight * R(Math.Sin(angle)); mass += weight.Abs();
            }
            var x = mass.Sign == 0 ? 0 : (real / mass).ToDouble(); var y = mass.Sign == 0 ? 0 : (imaginary / mass).ToDouble();
            var phase = Math.Abs(x) + Math.Abs(y) <= 1e-12 ? 90 : Math.Atan2(y, x) * (180 / Math.PI) + 90;
            if (phase < 0) phase += 360; if (phase >= 360) phase -= 360; if (phase < 1e-10 || phase > 360 - 1e-10) phase = 0; phases[i] = phase;
        }
        var means = externalAverage is not null ? externalAverage.ToArray() : kind is 1 or 2 or 3 or 6 ? SmoothStrengthStage(phases.Select(R).ToArray(), length, kind).Select(v => v.ToDouble()).ToArray() : Average(phases, length, kind); var signals = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++) { var distance = phases[i] - means[i]; var previous = i == 0 ? 0 : phases[i - 1] - means[i - 1]; signals[i] = distance < 0 ? distance < previous ? Signal.StrongBuy : Signal.Buy : distance > 0 ? distance > previous ? Signal.StrongSell : Signal.Sell : Signal.None; }
        return (new Dictionary<string, double[]> { { "Phase", phases }, { "Signal", means } }, signals);
    }
}
