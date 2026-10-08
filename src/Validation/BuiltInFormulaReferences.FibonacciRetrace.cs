using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> FibonacciRetraceOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return FibonacciRetraceValues(bars, Integer(options, "Length1", 15), Integer(options, "Length2", 50), Number(options, .382, "Factor"), AverageKind(options, 2)).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) FibonacciRetraceValues(IReadOnlyList<Bar> bars, int meanLength, int rangeLength, double factor, int kind)
    {
        if (double.IsNaN(factor) || double.IsInfinity(factor)) throw new ArgumentOutOfRangeException(nameof(factor));
        meanLength = Math.Max(1, meanLength); rangeLength = Math.Max(1, rangeLength);
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var f = R(factor); var complement = R(1) - f;
        var highest = bars.Select((_, i) => Window(bars, i, rangeLength).Max(b => b.High)).ToArray();
        var lowest = bars.Select((_, i) => Window(bars, i, rangeLength).Min(b => b.Low)).ToArray();
        var upper = highest.Select((v, i) => (complement * R(v) + f * R(lowest[i])).RoundExtendedBinary64()).ToArray();
        var lower = lowest.Select((v, i) => (complement * R(v) + f * R(highest[i])).RoundExtendedBinary64()).ToArray();
        var mean = SmoothRocBankStage(bars.Select(b => R(b.Close)).ToArray(), meanLength, kind);
        var trades = new Signal[bars.Count]; var oldBull = R(0); var oldBear = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var bull = mean[i] - upper[i]; var bear = mean[i] - lower[i];
            trades[i] = bull.Sign > 0 && bull.CompareTo(oldBull) > 0 ? Signal.StrongBuy : bear.Sign < 0 && bear.CompareTo(oldBear) < 0 ? Signal.StrongSell
                : bull.Sign > 0 ? Signal.Buy : bear.Sign < 0 ? Signal.Sell : Signal.None;
            oldBull = bull; oldBear = bear;
        }
        return (new Dictionary<string, double[]> { ["UpperBand"] = upper.Select(v => v.ToDouble()).ToArray(), ["LowerBand"] = lower.Select(v => v.ToDouble()).ToArray() }, trades);
    }
}
