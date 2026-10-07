using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> FearGreedOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return FearGreedValues(bars, Integer(options, "FastLength", 10), Integer(options, "SlowLength", 30), Integer(options, "SmoothLength", 2), AverageKind(options, 2)).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) FearGreedValues(IReadOnlyList<Bar> bars, int fast, int slow, int signalLength, int kind)
    {
        fast = Math.Max(1, fast); slow = Math.Max(1, slow); signalLength = Math.Max(1, signalLength);
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        ReferenceFraction Abs(ReferenceFraction v) => v.Sign < 0 ? R(0) - v : v;
        var up = new ReferenceFraction[bars.Count]; var down = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var bar = bars[i]; var previous = i == 0 ? bar.Close : bars[i - 1].Close;
            var candidates = new[] { R(bar.High) - R(bar.Low), Abs(R(bar.High) - R(previous)), Abs(R(bar.Low) - R(previous)) };
            var range = candidates.Aggregate((a, b) => a.CompareTo(b) > 0 ? a : b).RoundExtendedBinary64();
            up[i] = bar.Close > previous ? range : R(0); down[i] = bar.Close < previous ? range : R(0);
        }
        var fastUp = SmoothRocBankStage(up, fast, kind); var fastDown = SmoothRocBankStage(down, fast, kind);
        var slowUp = SmoothRocBankStage(up, slow, kind); var slowDown = SmoothRocBankStage(down, slow, kind);
        var line = Enumerable.Range(0, bars.Count).Select(i => ((fastUp[i] - fastDown[i]).RoundExtendedBinary64() - (slowUp[i] - slowDown[i]).RoundExtendedBinary64()).RoundExtendedBinary64()).ToArray();
        var signal = SmoothRocBankStage(line, signalLength, kind); var trades = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        { var v = signal[i]; var previous = i == 0 ? R(0) : signal[i - 1]; trades[i] = v.Sign > 0 && v.CompareTo(previous) > 0 ? Signal.StrongBuy : v.Sign < 0 && v.CompareTo(previous) < 0 ? Signal.StrongSell : v.Sign > 0 ? Signal.Buy : v.Sign < 0 ? Signal.Sell : Signal.None; }
        return (new Dictionary<string, double[]> { ["Fgi"] = line.Select(v => v.ToDouble()).ToArray(), ["Signal"] = signal.Select(v => v.ToDouble()).ToArray() }, trades);
    }
}
