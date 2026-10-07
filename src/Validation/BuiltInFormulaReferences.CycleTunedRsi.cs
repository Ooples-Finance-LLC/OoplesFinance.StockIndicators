using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] CycleTunedRsiValues(IReadOnlyList<Bar> bars, int length)
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
        var periods = AdaptiveCyberValues(bars, length, .07)["Period"]; var result = new double[bars.Count]; var zero = new ReferenceFraction(0); var one = new ReferenceFraction(1); var gain = zero; var loss = zero;
        for (var i = 0; i < bars.Count; i++)
        {
            var change = Round(R(bars[i].Close) - (i == 0 ? zero : R(bars[i - 1].Close))); var up = change.Sign > 0 ? change : zero; var down = change.Sign < 0 ? change.Abs() : zero;
            var weight = R(periods[i] == 0 ? .07 : 1 / periods[i]);
            gain = i == 0 ? up : Round(weight * up + (one - weight) * gain); loss = i == 0 ? down : Round(weight * down + (one - weight) * loss);
            result[i] = loss.Sign == 0 ? 100 : gain.Sign == 0 ? 0 : (new ReferenceFraction(100) * gain / (gain + loss)).ToDouble();
        }
        return result;
    }
}
