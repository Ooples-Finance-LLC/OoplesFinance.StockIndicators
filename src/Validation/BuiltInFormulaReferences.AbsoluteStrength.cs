using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> AbsoluteStrengthOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator) =>
        AbsoluteStrengthOutputs(bars, Math.Max(1, Integer(indicator.CreateOptions(), "Length", 10)), 21, 34);
    internal static IReadOnlyDictionary<string, double[]> AbsoluteStrengthOutputs(IReadOnlyList<Bar> bars, int length, int meanLength, int signalLength)
    {
        var zero = new ReferenceFraction(0); var one = new ReferenceFraction(1); var two = new ReferenceFraction(2);
        var gains = zero; var losses = zero; var ties = zero; var mean = zero; var first = zero; var second = zero;
        var values = new double[bars.Count];
        ReferenceFraction Round(ReferenceFraction value) => ReferenceFraction.FromDouble(value.ToDouble());
        ReferenceFraction Blend(ReferenceFraction value, ReferenceFraction previous, int period) => Round(previous + (value - previous) * two / new ReferenceFraction(period + 1L));
        for (var i = 0; i < bars.Count; i++)
        {
            var price = ReferenceFraction.FromDouble(bars[i].Close);
            if (i > 0)
            {
                var previous = ReferenceFraction.FromDouble(bars[i - 1].Close);
                if (bars[i].Close > bars[i - 1].Close) gains = (gains + (price / previous - one).RoundExtendedBinary64()).RoundExtendedBinary64();
                else if (bars[i].Close < bars[i - 1].Close) losses = (losses + (previous / price - one).RoundExtendedBinary64()).RoundExtendedBinary64();
                else ties = (ties + Round(one / new ReferenceFraction(length))).RoundExtendedBinary64();
            }
            var strength = (losses + ties).Sign == 0 ? one : Round((gains + ties) / (gains + losses + two * ties));
            mean = Blend(strength, mean, meanLength); var residual = Round(strength - mean);
            first = Blend(residual, first, signalLength); second = Blend(first, second, signalLength);
            var smooth = signalLength == 1 ? zero : Round(first + (first - second) * new ReferenceFraction(signalLength + 1L) / new ReferenceFraction(signalLength - 1L));
            values[i] = Round(residual - smooth).ToDouble();
        }
        return Outputs(("Asi", values));
    }
}
