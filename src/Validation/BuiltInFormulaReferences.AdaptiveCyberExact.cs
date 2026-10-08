using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> AdaptiveCyberValues(IReadOnlyList<Bar> bars, int length, double alpha) => AdaptiveCyberReference(bars.Select(b => b.Close).ToArray(), length, alpha);
    private static IReadOnlyDictionary<string, double[]> AdaptiveCyberReference(double[] prices, int length, double alpha)
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
        length = Math.Max(1, length); var zero = new ReferenceFraction(0); var one = new ReferenceFraction(1); var two = new ReferenceFraction(2); var four = new ReferenceFraction(4); var six = new ReferenceFraction(6);
        var smooth = new ReferenceFraction[prices.Length]; var cycle = new ReferenceFraction[prices.Length]; var quadrature = new ReferenceFraction[prices.Length]; var adaptive = new ReferenceFraction[prices.Length];
        var phases = new double[prices.Length]; var instant = new double[prices.Length]; var periods = new double[prices.Length]; var values = new double[prices.Length];
        ReferenceFraction Price(int i) => i < 0 ? zero : R(prices[i]);
        ReferenceFraction Prior(ReferenceFraction[] v, int i) => i < 0 ? zero : v[i];
        ReferenceFraction Filter(int i, ReferenceFraction[] previous, double coefficient)
        {
            var a = R(coefficient); var gain = (one - a / two) * (one - a / two); var pole = one - a;
            return Round(gain * (smooth[i] - two * Prior(smooth, i - 1) + Prior(smooth, i - 2)) + two * pole * Prior(previous, i - 1) - pole * pole * Prior(previous, i - 2));
        }
        for (var i = 0; i < prices.Length; i++)
        {
            smooth[i] = Round((Price(i) + two * Price(i - 1) + two * Price(i - 2) + Price(i - 3)) / six);
            var initial = Round((Price(i) - two * Price(i - 1) + Price(i - 2)) / four);
            cycle[i] = i < 7 ? initial : Filter(i, cycle, alpha);
            var dot = Round(R(.0962) * (cycle[i] - Prior(cycle, i - 6)) + R(.5769) * (Prior(cycle, i - 2) - Prior(cycle, i - 4)));
            quadrature[i] = Round(dot * R(.5 + .08 * (i == 0 ? 0 : instant[i - 1])));
            var inPhase = Prior(cycle, i - 3); var previousInPhase = Prior(cycle, i - 4); var previousQuadrature = Prior(quadrature, i - 1); var advance = .1;
            if (quadrature[i].Sign != 0 && previousQuadrature.Sign != 0)
            {
                var determinant = inPhase * previousQuadrature - previousInPhase * quadrature[i]; var inner = quadrature[i] * previousQuadrature + inPhase * previousInPhase;
                advance = inner.Sign == 0 ? determinant.Sign * quadrature[i].Sign * previousQuadrature.Sign > 0 ? 1.1 : .1 : Clamp((determinant / inner).ToDouble(), .1, 1.1);
            }
            phases[i] = advance; var sorted = phases.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(i + 1, length)).OrderBy(v => v).ToArray(); var median = (sorted[(sorted.Length - 1) / 2] + sorted[sorted.Length / 2]) / 2;
            var dominant = 6.28318 / median + .5;
            instant[i] = (R(.33) * R(dominant) + R(.67) * R(i == 0 ? 0 : instant[i - 1])).ToDouble();
            periods[i] = (R(.15) * R(instant[i]) + R(.85) * R(i == 0 ? 0 : periods[i - 1])).ToDouble();
            adaptive[i] = i < 7 ? initial : Filter(i, adaptive, 2 / (periods[i] + 1)); values[i] = adaptive[i].ToDouble();
        }
        return new Dictionary<string, double[]> { { "Eacc", values }, { "Period", periods } };
    }
}
