using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] LaguerreRsiValues(IReadOnlyList<Bar> bars, double gamma)
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
        gamma = Math.Max(0, Math.Min(1, gamma));
        var g = R(gamma); var gain = R(1 - gamma); var zero = new ReferenceFraction(0);
        var history = new ReferenceFraction[4][]; for (var stage = 0; stage < 4; stage++) history[stage] = new ReferenceFraction[bars.Count];
        var output = new double[bars.Count]; var baseline = bars.Count == 0 ? zero : R(bars[0].Close);
        ReferenceFraction Previous(int stage, int index) => index == 0 ? zero : history[stage][index - 1];
        for (var i = 0; i < bars.Count; i++)
        {
            history[0][i] = Round(gain * Round(R(bars[i].Close) - baseline) + g * Previous(0, i));
            for (var stage = 1; stage < 4; stage++) history[stage][i] = Round(Previous(stage - 1, i) + g * (Previous(stage, i) - history[stage - 1][i]));
            var variation = zero; var positive = zero;
            for (var stage = 0; stage < 3; stage++)
            { var delta = history[stage][i] - history[stage + 1][i]; variation += delta.Abs(); if (delta.Sign > 0) positive += delta; }
            output[i] = variation.Sign == 0 ? 0 : (positive / variation).ToDouble();
        }
        return output;
    }
}
