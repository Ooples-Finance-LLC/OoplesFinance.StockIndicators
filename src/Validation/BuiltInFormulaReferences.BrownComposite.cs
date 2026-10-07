using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> BrownCompositeOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); return BrownCompositeValues(bars, Integer(o, "FastLength", 13), Integer(o, "SlowLength", 33), Integer(o, "Length1", 14), Integer(o, "Length2", 9), Integer(o, "SmoothLength", 3), AverageKind(o, 1)).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) BrownCompositeValues(IReadOnlyList<Bar> bars, int fast, int slow, int length, int lag, int smooth, int kind, double[][]? external = null)
    {
        fast = Math.Max(1, fast); slow = Math.Max(1, slow); length = Math.Max(1, length); lag = Math.Max(1, lag); smooth = Math.Max(1, smooth);
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var rsi = RoundedPriceRsi(bars, length, 6); var second = RoundedPriceRsi(bars, smooth, 6);
        double[] Mean(double[] values, int period) => kind is 1 or 2 or 3 or 6 ? SmoothRocBankStage(values.Select(R).ToArray(), period, kind).Select(v => v.ToDouble()).ToArray() : Average(values, period, kind);
        var level = external is null ? Mean(second, smooth) : external[0];
        var line = rsi.Select((v, i) => (R(i < lag ? 0 : (R(v) - R(rsi[i - lag])).ToDouble()) + R(level[i])).ToDouble()).ToArray();
        var fastLine = external is null ? Mean(line, fast) : external[1]; var slowLine = external is null ? Mean(line, slow) : external[2];
        var events = new Signal[bars.Count]; var oldBull = R(0); var oldBear = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var bull = R(line[i]) - R(Math.Max(fastLine[i], slowLine[i])); var bear = R(line[i]) - R(Math.Min(fastLine[i], slowLine[i]));
            events[i] = bull.Sign > 0 && bull.CompareTo(oldBull) > 0 ? Signal.StrongBuy : bear.Sign < 0 && bear.CompareTo(oldBear) < 0 ? Signal.StrongSell
                : bull.Sign > 0 ? Signal.Buy : bear.Sign < 0 ? Signal.Sell : Signal.None;
            oldBull = bull; oldBear = bear;
        }
        return (new Dictionary<string, double[]> { ["Cbci"] = line, ["FastSignal"] = fastLine, ["SlowSignal"] = slowLine }, events);
    }
}
