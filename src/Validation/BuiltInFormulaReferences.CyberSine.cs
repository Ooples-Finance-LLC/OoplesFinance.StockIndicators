using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) CyberSineValues(IReadOnlyList<Bar> bars, int length, double alpha)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var chunk = new ReferenceFraction(BigInteger.One << 512); var upper = new ReferenceFraction(BigInteger.One << 256); var lower = new ReferenceFraction(1) / upper;
        ReferenceFraction Round(ReferenceFraction value)
        {
            if (value.Sign == 0) return value; var scale = new ReferenceFraction(1);
            while (value.Abs().CompareTo(lower) < 0) { value *= chunk; scale /= chunk; }
            while (value.Abs().CompareTo(upper) >= 0) { value /= chunk; scale *= chunk; }
            return R(value.ToDouble()) * scale;
        }
        var periods = AdaptiveCyberValues(bars, length, alpha)["Period"]; var smooth = new ReferenceFraction[bars.Count]; var cycles = new ReferenceFraction[bars.Count]; var sine = new double[bars.Count]; var lead = new double[bars.Count]; var signals = new Signal[bars.Count]; var a = R(.07); var gain = (R(1) - a / R(2)) * (R(1) - a / R(2)); var retention = R(1) - a;
        ReferenceFraction Price(int i) => i < 0 ? R(0) : R(bars[i].Close);
        ReferenceFraction At(ReferenceFraction[] values, int i) => i < 0 ? R(0) : values[i];
        for (var i = 0; i < bars.Count; i++)
        {
            smooth[i] = Round((Price(i) + R(2) * Price(i - 1) + R(2) * Price(i - 2) + Price(i - 3)) / R(6));
            cycles[i] = i < 7 ? Round((Price(i) - R(2) * Price(i - 1) + Price(i - 2)) / R(4)) : Round(gain * (smooth[i] - R(2) * At(smooth, i - 1) + At(smooth, i - 2)) + R(2) * retention * At(cycles, i - 1) - retention * retention * At(cycles, i - 2));
            var nearest = Math.Round(periods[i]); var count = Math.Max(1, (int)(Math.Abs(periods[i] - nearest) <= 1e-9 * Math.Max(1, Math.Abs(periods[i])) ? nearest : Math.Ceiling(periods[i])));
            var real = R(0); var imaginary = R(0); var scale = R(0);
            for (var lag = 0; lag < Math.Min(count, i + 1); lag++) { var angle = 2 * Math.PI * ((double)lag / count); real += R(Math.Sin(angle)) * cycles[i - lag]; imaginary += R(Math.Cos(angle)) * cycles[i - lag]; scale += cycles[i - lag].Abs(); }
            if (scale.Sign != 0) { if (Math.Abs((real / scale).ToDouble()) <= 64 * Math.Pow(2, -52)) real = R(0); if (Math.Abs((imaginary / scale).ToDouble()) <= 64 * Math.Pow(2, -52)) imaginary = R(0); }
            var phase = imaginary.Abs().CompareTo(R(.001)) > 0 ? Math.Atan((real / imaginary).ToDouble()) * (180 / Math.PI) : 90 * real.Sign; phase += 90; if (imaginary.Sign < 0) phase += 180; if (phase > 315) phase -= 360;
            sine[i] = Math.Sin(phase * (Math.PI / 180)); lead[i] = Math.Sin((phase + 45) * (Math.PI / 180)); var current = R(sine[i]) - R(lead[i]); var previous = i == 0 ? R(0) : R(sine[i - 1]) - R(lead[i - 1]);
            signals[i] = current.Sign > 0 ? current.CompareTo(previous) > 0 ? Signal.StrongBuy : Signal.Buy : current.Sign < 0 ? current.CompareTo(previous) < 0 ? Signal.StrongSell : Signal.Sell : Signal.None;
        }
        return (new Dictionary<string, double[]> { { "Sine", sine }, { "LeadSine", lead } }, signals);
    }
}
