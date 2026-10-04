using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] DeviationScaledValues(IReadOnlyList<Bar> bars, int fast, int slow, bool fisher = false)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        ReferenceFraction Round(ReferenceFraction value) => DeviationReferenceRound(value);
        fast = Math.Max(2, fast); slow = Math.Max(1, slow);
        var angle = Math.Sqrt(2) * Math.PI / fast; var radius = R(Math.Exp(-angle)); var cosine = R(Math.Cos(angle));
        var one = new ReferenceFraction(1); var two = new ReferenceFraction(2); var zero = new ReferenceFraction(0);
        var c2 = two * radius * cosine; var c3 = zero - radius * radius; var c1 = one - c2 - c3;
        var changes = new ReferenceFraction[bars.Count]; var drive = new ReferenceFraction[bars.Count]; var filtered = new ReferenceFraction[bars.Count]; var average = new ReferenceFraction[bars.Count]; var result = new double[bars.Count]; var ratio = zero; var held = 0d;
        ReferenceFraction Prior(ReferenceFraction[] values, int index) => index < 0 ? zero : values[index];
        ReferenceFraction Root(ReferenceFraction variance) => DeviationReferenceRoot(variance);
        for (var i = 0; i < bars.Count; i++)
        {
            changes[i] = i < 2 ? zero : Round(R(bars[i].Close) - R(bars[i - 2].Close));
            drive[i] = Round((changes[i] + Prior(changes, i - 1)) / two);
            filtered[i] = Round(c1 * (drive[i] + Prior(drive, i - 1)) / two + c2 * Prior(filtered, i - 1) + c3 * Prior(filtered, i - 2));
            if (i >= slow - 1)
            {
                var sum = zero; var squares = zero;
                for (var j = i - slow + 1; j <= i; j++) { sum += filtered[j]; squares += filtered[j] * filtered[j]; }
                var count = new ReferenceFraction(slow); var deviation = Root((count * squares - sum * sum) / (count * count));
                if (deviation.Sign != 0) ratio = Round(filtered[i] / deviation);
            }
            var gain = R(Clamp((new ReferenceFraction(5) * ratio.Abs() / new ReferenceFraction(slow)).ToDouble(), .01, .99));
            average[i] = Round(gain * R(bars[i].Close) + (one - gain) * Prior(average, i - 1)); var value = average[i].ToDouble();
            result[i] = fisher ? held = Math.Abs(value) < 2 ? FisherReferenceTransform(value / 2) : held : value;
        }
        return result;
    }
}
