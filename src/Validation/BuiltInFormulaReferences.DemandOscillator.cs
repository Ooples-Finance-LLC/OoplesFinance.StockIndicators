using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> DemandOscillatorOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
        => DemandOscillatorValues(bars, AverageKind(indicator.CreateOptions(), 3), 10, 2, 20).Outputs;
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) DemandOscillatorValues(IReadOnlyList<Bar> bars, int kind, int averageLength, int rangeLength, int lineLength)
    {
        averageLength = Math.Max(1, averageLength); rangeLength = Math.Max(1, rangeLength); lineLength = Math.Max(1, lineLength);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        ReferenceFraction[] Mean(ReferenceFraction[] values, int period) => kind is 1 or 2 or 3 or 6 ? SmoothRocBankStage(values, period, kind)
            : Average(values.Select(v => v.ToDouble()).ToArray(), period, kind).Select(R).ToArray();
        var ranges = bars.Select((_, i) =>
        {
            var start = (int)Math.Max(0L, i - (long)rangeLength + 1); var window = bars.Skip(start).Take(i - start + 1);
            return (R(window.Max(b => b.High)) - R(window.Min(b => b.Low))).RoundExtendedBinary64();
        }).ToArray();
        var average = Mean(ranges, averageLength);
        var imbalance = bars.Select((b, i) =>
        {
            var current = R(b.Close); var previous = i == 0 ? R(0) : R(bars[i - 1].Close); var volume = R(b.Volume);
            var change = (current - previous).RoundExtendedBinary64(); var absolute = previous.Sign < 0 ? R(0) - previous : previous;
            var percent = previous.Sign == 0 ? R(0) : ((change / absolute).RoundExtendedBinary64() * R(100)).RoundExtendedBinary64();
            var k = average[i].Sign == 0 ? R(0) : ((R(3) * current).RoundExtendedBinary64() / average[i]).RoundExtendedBinary64();
            var product = (percent * k).RoundExtendedBinary64(); var reciprocal = product.Sign == 0 ? R(0) : (volume / product).RoundExtendedBinary64();
            return (current.CompareTo(previous) > 0 ? volume - reciprocal : reciprocal - volume).RoundExtendedBinary64();
        }).ToArray();
        var line = Mean(imbalance, lineLength); var signal = Mean(line, averageLength); var trades = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var previous = i == 0 ? R(0) : signal[i - 1]; var previousTwo = i < 2 ? R(0) : signal[i - 2];
            var slope = signal[i] - previous; var oldSlope = previous - previousTwo;
            trades[i] = slope.Sign > 0 && slope.CompareTo(oldSlope) > 0 ? Signal.StrongBuy : slope.Sign < 0 && slope.CompareTo(oldSlope) < 0 ? Signal.StrongSell
                : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        }
        return (new Dictionary<string, double[]> { ["Do"] = line.Select(v => v.ToDouble()).ToArray(), ["Signal"] = signal.Select(v => v.ToDouble()).ToArray() }, trades);
    }
}
