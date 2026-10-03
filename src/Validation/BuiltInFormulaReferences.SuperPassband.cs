using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> SuperPassbandOutputs(IReadOnlyList<Bar> bars, int fast, int slow, int numerator, int length)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        ReferenceFraction Round(ReferenceFraction value) => RoundRocBankStage(value);
        numerator = Math.Max(1, numerator); length = Math.Max(1, length);
        var a = Math.Max(.01, Math.Min(.99, (double)numerator / Math.Max(1, fast)));
        var b = Math.Max(.01, Math.Min(.99, (double)numerator / Math.Max(1, slow)));
        var gain = R(a - b); var fastPole = R(1 - a); var slowPole = R(1 - b);
        var first = new ReferenceFraction[bars.Count]; var second = new ReferenceFraction[bars.Count]; var squares = new ReferenceFraction[bars.Count];
        var upper = new double[bars.Count]; var zero = new ReferenceFraction(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var change = Round(R(bars[i].Close) - (i == 0 ? zero : R(bars[i - 1].Close)));
            first[i] = Round(gain * change + fastPole * (i == 0 ? zero : first[i - 1]));
            second[i] = Round(first[i] + slowPole * (i == 0 ? zero : second[i - 1])); squares[i] = second[i] * second[i];
            var sum = zero; for (var j = Math.Max(0, i - length + 1); j <= i; j++) sum += squares[j];
            upper[i] = (sum / new ReferenceFraction(Math.Min((long)length, i + 1L))).SqrtToDouble();
        }
        return new() { { "Espf", second.Select(v => v.ToDouble()).ToArray() }, { "UpperBand", upper }, { "LowerBand", upper.Select(v => -v).ToArray() } };
    }
}
