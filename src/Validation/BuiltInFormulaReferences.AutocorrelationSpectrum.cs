using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) AutocorrelationSpectrumValues(IReadOnlyList<Bar> bars, int upper, int lower, int firstLag)
    {
        upper = Math.Max(1, upper); lower = Math.Max(1, lower); firstLag = Math.Max(0, firstLag); var source = RoofAutocorrelationValues(bars, upper, lower); var correlation = source.Outputs["Eaci"]; var values = new double[bars.Count]; var powers = new Dictionary<int, double>();
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        for (var i = 0; i < bars.Count; i++)
        {
            var active = powers.Count != 0; for (var lag = firstLag; !active && lag <= upper && lag <= i; lag++) active = correlation[i - lag] != 0;
            if (!active || lower > upper) continue;
            var next = new Dictionary<int, double>();
            for (long period = lower; period <= upper; period++)
            {
                var real = R(0); var imaginary = R(0);
                for (var lag = firstLag; lag <= upper && lag <= i; lag++) { var angle = 2 * Math.PI * ((double)lag / period); real += R(correlation[i - lag]) * R(Math.Cos(angle)); imaginary += R(correlation[i - lag]) * R(Math.Sin(angle)); }
                real = R(real.ToDouble()); imaginary = R(imaginary.ToDouble()); var square = R((real * real + imaginary * imaginary).ToDouble()); var forcing = R((square * square).ToDouble()); powers.TryGetValue((int)period, out var previous); next[(int)period] = (R(.2) * forcing + R(.8) * R(previous)).ToDouble();
            }
            var peak = next.Values.DefaultIfEmpty(0).Max(); var weighted = R(0); var total = R(0);
            foreach (var entry in next) { var power = peak == 0 ? 0 : entry.Value / peak; if (power >= .5) { weighted += R(entry.Key) * R(power); total += R(power); } }
            values[i] = total.Sign == 0 ? 0 : (weighted / total).ToDouble(); powers = next;
        }
        return (new Dictionary<string, double[]> { { "Eacp", values } }, source.Signals);
    }
}
