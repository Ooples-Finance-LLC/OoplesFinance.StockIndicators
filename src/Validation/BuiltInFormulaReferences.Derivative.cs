using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] DerivativeOutputs(IReadOnlyList<Bar> bars, int rsi, int mean, int first, int second, int kind = 3)
    {
        var source = RoundedPriceRsi(bars, Math.Max(1, rsi), kind).Select(ReferenceFraction.FromDouble).ToArray();
        var line = SmoothStrengthStage(SmoothStrengthStage(source, Math.Max(1, first), kind), Math.Max(1, second), kind).Select(v => v.ToDouble()).ToArray();
        return line.Select((v, i) =>
        {
            var start = Math.Max(0, i - Math.Max(1, mean) + 1); var sum = new ReferenceFraction(0);
            for (var j = start; j <= i; j++) sum += ReferenceFraction.FromDouble(line[j]);
            var average = (sum / new ReferenceFraction(i - start + 1)).ToDouble();
            return (ReferenceFraction.FromDouble(v) - ReferenceFraction.FromDouble(average)).ToDouble();
        }).ToArray();
    }
}
