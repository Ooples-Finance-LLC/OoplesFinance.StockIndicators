using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) MamaDerivedValues(IReadOnlyList<Bar> bars, int mode)
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
        var mama = MamaValues(bars.Select(b => b.Close).ToArray()).Outputs; var smooth = mama["Smooth"]; var periods = mama["SmoothPeriod"]; var first = new ReferenceFraction[bars.Count]; var second = new ReferenceFraction[bars.Count]; var quadrature = new ReferenceFraction[bars.Count]; var signals = new Signal[bars.Count];
        ReferenceFraction At(ReferenceFraction[] values, int i) => i < 0 ? R(0) : values[i];
        for (var i = 0; i < bars.Count; i++)
        {
            var count = Math.Max(1, (int)Math.Ceiling(periods[i] + .5)); ReferenceFraction current, previous;
            if (mode == 0)
            {
                var real = R(0); var imaginary = R(0); var scale = R(0);
                for (var lag = 0; lag < Math.Min(count, i + 1); lag++) { var angle = 2 * Math.PI * ((double)lag / count); real += R(Math.Sin(angle)) * R(smooth[i - lag]); imaginary += R(Math.Cos(angle)) * R(smooth[i - lag]); scale += R(smooth[i - lag]).Abs(); }
                if (scale.Sign != 0) { if (Math.Abs((real / scale).ToDouble()) <= 64 * Math.Pow(2, -52)) real = R(0); if (Math.Abs((imaginary / scale).ToDouble()) <= 64 * Math.Pow(2, -52)) imaginary = R(0); }
                var phase = imaginary.Abs().CompareTo(R(.001)) > 0 ? Math.Atan((real / imaginary).ToDouble()) * (180 / Math.PI) : 90 * real.Sign; phase += 90; phase += periods[i] != 0 ? 360 / periods[i] : 0; if (imaginary.Sign < 0) phase += 180; if (phase > 315) phase -= 360;
                first[i] = R(Math.Sin(phase * (Math.PI / 180))); second[i] = R(Math.Sin((phase + 45) * (Math.PI / 180))); current = first[i] - second[i]; previous = At(first, i - 1) - At(second, i - 1);
            }
            else if (mode == 1)
            {
                var delta = Round(R(smooth[i]) - R(i < 2 ? 0 : smooth[i - 2])); quadrature[i] = Round(delta * R(.5 * (.1759 * periods[i] + .4607)));
                var realCount = Math.Max(1, (int)Math.Ceiling(periods[i] / 2)); var imaginaryCount = Math.Max(1, (int)Math.Ceiling(periods[i] / 4));
                ReferenceFraction Sum(int length) => Enumerable.Range(0, Math.Min(length, i + 1)).Select(lag => quadrature[i - lag]).Aggregate(R(0), (sum, value) => sum + value);
                first[i] = Round(R(1.57) * Sum(realCount) / R(realCount)); second[i] = Round(R(1.25) * Sum(imaginaryCount) / R(imaginaryCount)); current = second[i] - first[i]; previous = At(second, i - 1) - At(first, i - 1);
            }
            else
            {
                first[i] = Round(Enumerable.Range(0, Math.Min(count, i + 1)).Select(lag => R(bars[i - lag].Close)).Aggregate(R(0), (sum, value) => sum + value) / R(count)); second[i] = Round((R(4) * first[i] + R(3) * At(first, i - 1) + R(2) * At(first, i - 2) + At(first, i - 3)) / R(10)); current = R(bars[i].Close) - second[i]; previous = R(i == 0 ? 0 : bars[i - 1].Close) - At(second, i - 1);
            }
            signals[i] = current.Sign > 0 ? current.CompareTo(previous) > 0 ? Signal.StrongBuy : Signal.Buy : current.Sign < 0 ? current.CompareTo(previous) < 0 ? Signal.StrongSell : Signal.Sell : Signal.None;
        }
        return (new Dictionary<string, double[]> { { mode == 0 ? "Sine" : mode == 1 ? "I3" : "Eit", first.Select(v => v.ToDouble()).ToArray() }, { mode == 0 ? "LeadSine" : mode == 1 ? "IQ" : "Signal", second.Select(v => v.ToDouble()).ToArray() } }, signals);
    }
}
