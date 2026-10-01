using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> KaseConvergenceOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return Outputs(("Kcd", KaseConvergenceValues(bars, Integer(options, "Length1", 30), Integer(options, "Length2", 3), Integer(options, "Length3", 8), AverageKind(options, 1), (indicator as IIndicator)?.Source is not null).Values));
    }
    internal static (double[] Values, Signal[] Trades) KaseConvergenceValues(IReadOnlyList<Bar> bars, int length, int peakLength, int signalLength, int kind, bool selected = false)
    {
        length = Math.Max(1, length); peakLength = Math.Max(1, peakLength); signalLength = Math.Max(1, signalLength);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value); var zero = R(0);
        var highs = bars.Select((b, i) => !selected || b.Close >= b.Low && b.Close <= b.High ? b.High : Math.Max(b.Close, bars[Math.Max(0, i - 1)].Close)).ToArray();
        var lows = bars.Select((b, i) => !selected || b.Close >= b.Low && b.Close <= b.High ? b.Low : Math.Min(b.Close, bars[Math.Max(0, i - 1)].Close)).ToArray();
        var ranges = bars.Select((b, i) =>
        {
            var previous = R(bars[Math.Max(0, i - 1)].Close); var span = R(highs[i]) - R(lows[i]);
            var highGap = (R(highs[i]) - previous).Abs(); var lowGap = (R(lows[i]) - previous).Abs();
            var range = span.CompareTo(highGap) >= 0 ? span : highGap; if (range.CompareTo(lowGap) < 0) range = lowGap;
            return RoundRocBankStage(range);
        }).ToArray();
        var atr = SmoothRocBankStage(ranges, length, 6); var root = R(Math.Sqrt(length));
        var drive = bars.Select((_, i) => atr[i].Sign == 0 ? zero : RoundRocBankStage((R(highs[i]) + R(lows[i])
            - (i < length ? zero : R(highs[i - length]) + R(lows[i - length]))) * root / atr[i])).ToArray();
        var peak = SmoothRocBankStage(drive, peakLength, 2);
        var signal = kind is 1 or 2 or 3 or 6 ? SmoothRocBankStage(peak, signalLength, kind) : Average(peak.Select(v => v.ToDouble()).ToArray(), signalLength, kind).Select(R).ToArray();
        var residual = peak.Select((v, i) => RoundRocBankStage(v - signal[i])).ToArray(); var trades = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var value = residual[i]; var previous = i == 0 ? zero : residual[i - 1];
            trades[i] = value.Sign > 0 && value.CompareTo(previous) > 0 ? Signal.StrongBuy : value.Sign < 0 && value.CompareTo(previous) < 0 ? Signal.StrongSell : value.Sign > 0 ? Signal.Buy : value.Sign < 0 ? Signal.Sell : Signal.None;
        }
        return (residual.Select(v => v.ToDouble()).ToArray(), trades);
    }
}
