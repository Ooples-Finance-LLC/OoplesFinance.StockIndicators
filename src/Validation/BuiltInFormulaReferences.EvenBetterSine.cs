using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] EvenBetterSineValues(IReadOnlyList<Bar> bars, int highPeriod, int lowPeriod)
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
        var highAngle = Math.Max(.01, Math.Min(.99, 2 * Math.PI / Math.Max(1, highPeriod))); var pole = Math.Cos(highAngle) / (1 + Math.Sin(highAngle)); var gain = R((1 + pole) / 2);
        var lowAngle = Math.Max(.01, Math.Min(.99, 1.414 * Math.PI / Math.Max(1, lowPeriod))); var radius = Math.Exp(-lowAngle); var c2 = 2 * radius * Math.Cos(lowAngle); var c3 = -radius * radius; var c1 = R(1 - c2 - c3);
        var zero = new ReferenceFraction(0); var changes = new ReferenceFraction[bars.Count]; var high = new ReferenceFraction[bars.Count]; var filtered = new ReferenceFraction[bars.Count]; var output = new double[bars.Count];
        ReferenceFraction At(ReferenceFraction[] values, int i) => i < 0 ? zero : values[i];
        var powerChunk = new ReferenceFraction(BigInteger.One << 1024); var powerUpper = chunk; var powerLower = new ReferenceFraction(1) / powerUpper;
        for (var i = 0; i < bars.Count; i++)
        {
            changes[i] = i == 0 ? zero : Round(R(bars[i].Close) - R(bars[i - 1].Close));
            high[i] = Round(gain * Round((changes[i] + At(changes, i - 1)) / new ReferenceFraction(2)) + R(pole) * At(high, i - 1));
            filtered[i] = Round(c1 * high[i] + R(c2) * At(filtered, i - 1) + R(c3) * At(filtered, i - 2));
            var total = zero; var power = zero;
            for (var lag = 0; lag < 3; lag++) { var v = At(filtered, i - lag); total += v; power += new ReferenceFraction(3) * v * v; }
            var rootScale = new ReferenceFraction(1);
            if (power.Sign == 0) { output[i] = 0; continue; }
            while (power.CompareTo(powerLower) < 0) { power *= powerChunk; rootScale /= chunk; }
            while (power.CompareTo(powerUpper) >= 0) { power /= powerChunk; rootScale *= chunk; }
            output[i] = (total / (R(power.SqrtToDouble()) * rootScale)).ToDouble();
        }
        return output;
    }
}
