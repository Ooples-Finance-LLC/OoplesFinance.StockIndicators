using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ClampedBandPassOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); var variant = indicator.BatchName == IndicatorName.EhlersBandPassFilterV1 ? 0 : indicator.BatchName == IndicatorName.EhlersBandPassFilterV2 ? 1 : 2; return ClampedBandPassOutputs(bars, Integer(o, "Length", 20), Number(o, variant == 2 ? .1 : .3, variant == 2 ? "Delta" : "Bw"), variant); }
    internal static IReadOnlyDictionary<string, double[]> ClampedBandPassOutputs(IReadOnlyList<Bar> bars, int length, double bandwidth, int variant)
    {
        length = Math.Max(1, length); double Clamp(double x) => x < .01 ? .01 : x > .99 ? .99 : x; ReferenceFraction F(double x) => ReferenceFraction.FromDouble(x); var one = new ReferenceFraction(1); var two = new ReferenceFraction(2);
        var beta = F(Math.Cos(Clamp(2 * Math.PI / length))); double pole;
        if (variant == 1) { var c = Math.Cos(Clamp(bandwidth * 2 * Math.PI / length)); pole = 1 / c - Math.Sqrt(1 / (c * c) - 1); }
        else { var g = 1 / Math.Cos(Clamp((variant == 2 ? 4 : 2) * Math.PI * bandwidth / length)); pole = g - Math.Sqrt(g * g - 1); }
        var alpha = F(pole); var hpAngle = Clamp(.25 * bandwidth * 2 * Math.PI / length); var triggerAngle = Clamp(1.5 * bandwidth * 2 * Math.PI / length); var hpAlpha = F((Math.Cos(hpAngle) + Math.Sin(hpAngle) - 1) / Math.Cos(hpAngle)); var triggerAlpha = F((Math.Cos(triggerAngle) + Math.Sin(triggerAngle) - 1) / Math.Cos(triggerAngle));
        var prices = bars.Select(b => F(b.Close)).ToArray(); var hp = new ReferenceFraction[bars.Count]; var band = new ReferenceFraction[bars.Count]; var peak = new ReferenceFraction[bars.Count]; var line = new double[bars.Count]; var trigger = new ReferenceFraction[bars.Count]; var zero = new ReferenceFraction(0);
        for (var i = 0; i < bars.Count; i++)
        {
            hp[i] = variant != 0 ? prices[i] : ((one + hpAlpha / two) * (i == 0 ? zero : prices[i] - prices[i - 1]) + (one - hpAlpha) * (i == 0 ? zero : hp[i - 1])).RoundExtendedBinary64();
            band[i] = i < (variant == 2 ? 2 : 3) ? zero : (((one - alpha) / two) * (hp[i] - hp[i - 2]) + beta * (one + alpha) * band[i - 1] - alpha * band[i - 2]).RoundExtendedBinary64();
            if (variant == 0)
            {
                var decayed = (F(.991) * (i == 0 ? zero : peak[i - 1])).RoundExtendedBinary64(); peak[i] = decayed.CompareTo(band[i].Abs()) > 0 ? decayed : band[i].Abs(); line[i] = peak[i].Sign == 0 ? 0 : (band[i] / peak[i]).ToDouble(); trigger[i] = ((one + triggerAlpha / two) * (F(line[i]) - (i == 0 ? zero : F(line[i - 1]))) + (one - triggerAlpha) * (i == 0 ? zero : trigger[i - 1])).RoundExtendedBinary64();
            }
            else { line[i] = band[i].ToDouble(); trigger[i] = zero; }
        }
        return variant == 0 ? Outputs(("Ebpf", line), ("Signal", trigger.Select(v => v.ToDouble()).ToArray())) : Outputs((variant == 1 ? "Ebpf" : "Ecbpf", line));
    }
}
