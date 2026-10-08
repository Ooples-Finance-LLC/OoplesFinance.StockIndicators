using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> DecyclerOscillatorOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var length = Math.Max(1, Integer(indicator.CreateOptions(), "Length", 100));
        return Outputs(("FastEdo", DecyclerOscillatorLane(bars, length, 1.2)), ("SlowEdo", DecyclerOscillatorLane(bars, length, 1, 2)));
    }
    internal static double[] DecyclerOscillatorLane(IReadOnlyList<Bar> bars, int length, double multiplier, double periodScale = 1)
    {
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var first = HighPassStates(prices, length, periodScale);
        var middle = prices.Select((p, i) => (p - first[i]).RoundExtendedBinary64()).ToArray();
        var second = HighPassStates(middle, length, periodScale / 2);
        var factor = (ReferenceFraction.FromDouble(multiplier) * new ReferenceFraction(100)).RoundExtendedBinary64();
        return prices.Select((p, i) => bars[i].Close == 0 ? 0 : (second[i] * factor / p).ToDouble()).ToArray();
    }
}
