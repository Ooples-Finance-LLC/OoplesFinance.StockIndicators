using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? SelfAdjustingLaguerreFormula(IBuiltInIndicator indicator)
    {
        if (indicator.BatchName != IndicatorName.EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha) return null;
        var length = Integer(indicator.CreateOptions(), "Length");
        return new("Elrsiwsa", new[] { "Elrsiwsa" }, bars => Outputs(("Elrsiwsa", SelfAdjustingLaguerreValues(bars, length))));
    }
    internal static double[] SelfAdjustingLaguerreValues(IReadOnlyList<Bar> bars, int length)
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
        length = Math.Max(1, length); var zero = new ReferenceFraction(0); var two = new ReferenceFraction(2);
        var ratios = new ReferenceFraction[bars.Count]; var stages = new ReferenceFraction[4][];
        for (var stage = 0; stage < 4; stage++) stages[stage] = new ReferenceFraction[bars.Count];
        var output = new double[bars.Count];
        ReferenceFraction Prior(int stage, int index) => index == 0 ? zero : stages[stage][index - 1];
        for (var i = 0; i < bars.Count; i++)
        {
            var bar = bars[i]; var previous = i == 0 ? 0 : bars[i - 1].Close; var start = Math.Max(0, i - length + 1);
            var highest = bars.Skip(start).Take(i - start + 1).Max(b => b.High); var lowest = bars.Skip(start).Take(i - start + 1).Min(b => b.Low);
            var hc = R(Math.Max(bar.High, previous)); var lc = R(Math.Min(bar.Low, previous)); var range = Round(R(highest) - R(lowest));
            ratios[i] = range.Sign == 0 ? zero : Round(Round(hc - lc) / range);
            var sum = zero; for (var j = start; j <= i; j++) sum += ratios[j]; sum = Round(sum);
            var normalized = sum; var exponent = 0;
            if (sum.Sign != 0)
            {
                while (normalized.Abs().CompareTo(lower) < 0) { normalized *= chunk; exponent -= 512; }
                while (normalized.Abs().CompareTo(upper) >= 0) { normalized /= chunk; exponent += 512; }
            }
            var gain = sum.Sign <= 0 ? .01 : length == 1 ? .99 : Clamp((Math.Log(normalized.ToDouble()) + exponent * Math.Log(2)) / Math.Log(length), .01, .99);
            var source = Round((R(bar.Open) + R(previous) + two * hc + two * lc + two * R(bar.Close)) / new ReferenceFraction(8));
            var g = R(gain); var one = new ReferenceFraction(1);
            stages[0][i] = Round(g * source + (one - g) * Prior(0, i));
            for (var stage = 1; stage < 4; stage++) stages[stage][i] = Round(Prior(stage - 1, i) + (one - g) * (Prior(stage, i) - stages[stage - 1][i]));
            var variation = zero; var positive = zero; var scale = zero;
            for (var stage = 0; stage < 4; stage++)
            {
                if (stages[stage][i].Abs().CompareTo(scale) > 0) scale = stages[stage][i].Abs();
                if (stage == 3) continue;
                var delta = stages[stage][i] - stages[stage + 1][i]; variation += delta.Abs(); if (delta.Sign > 0) positive += delta;
            }
            output[i] = variation.CompareTo(R(1.4210854715202004e-14) * scale) <= 0 ? 0 : (positive / variation).ToDouble();
        }
        return output;
    }
}
