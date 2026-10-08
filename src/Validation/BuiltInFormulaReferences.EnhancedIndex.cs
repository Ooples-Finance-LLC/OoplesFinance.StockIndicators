using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> EnhancedIndexOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
        => EnhancedIndexValues(bars, Integer(indicator.CreateOptions(), "Length", 14), Integer(indicator.CreateOptions(), "SignalLength", 8), AverageKind(indicator.CreateOptions(), 1)).Outputs;
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) EnhancedIndexValues(IReadOnlyList<Bar> bars, int length, int signalLength, int kind, double[]? externalMean = null, double[]? externalSignal = null)
    {
        length = Math.Max(1, length); signalLength = Math.Max(1, signalLength);
        var meanLength = (int)Math.Max(2L, Math.Min(530L, ((long)length + 1) / 2));
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        ReferenceFraction[] Mean(ReferenceFraction[] values, int period) => kind is 1 or 2 or 3 or 6 ? SmoothRocBankStage(values, period, kind)
            : Average(values.Select(v => v.ToDouble()).ToArray(), period, kind).Select(R).ToArray();
        var means = externalMean is null ? Mean(bars.Select(b => R(b.Close)).ToArray(), meanLength) : externalMean.Select(R).ToArray();
        var lines = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var start = (int)Math.Max(0L, i - (long)length + 1); var observations = bars.Skip(start).Take(i-start+1).ToArray();
            var range = R(observations.Max(b => b.High)) - R(observations.Min(b => b.Low));
            lines[i] = range.Sign == 0 ? R(0) : (R(2) * (R(bars[i].Close) - means[i]) / range).RoundExtendedBinary64();
        }
        var signal = externalSignal is null ? Mean(lines, signalLength) : externalSignal.Select(R).ToArray();
        var trades = new Signal[bars.Count]; var previous = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var spread = lines[i] - signal[i];
            trades[i] = spread.Sign > 0 && spread.CompareTo(previous) > 0 ? Signal.StrongBuy : spread.Sign < 0 && spread.CompareTo(previous) < 0 ? Signal.StrongSell
                : spread.Sign > 0 ? Signal.Buy : spread.Sign < 0 ? Signal.Sell : Signal.None;
            previous = spread;
        }
        return (new Dictionary<string, double[]> { ["Ei"] = lines.Select(v => v.ToDouble()).ToArray(), ["Signal"] = signal.Select(v => v.ToDouble()).ToArray() }, trades);
    }
}
