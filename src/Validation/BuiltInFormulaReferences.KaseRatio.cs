using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> KaseRatioOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); return KaseRatioValues(bars, Integer(options, "Length", 10), AverageKind(options, 1), (indicator as IIndicator)?.Source is not null).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Trades) KaseRatioValues(IReadOnlyList<Bar> bars, int length, int kind, bool selected = false)
    {
        length = Math.Max(1, length); ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value); var zero = R(0); var root = R(Math.Sqrt(length));
        var highs = bars.Select((b, i) => !selected || b.Close >= b.Low && b.Close <= b.High ? b.High : Math.Max(b.Close, bars[Math.Max(0, i - 1)].Close)).ToArray();
        var lows = bars.Select((b, i) => !selected || b.Close >= b.Low && b.Close <= b.High ? b.Low : Math.Min(b.Close, bars[Math.Max(0, i - 1)].Close)).ToArray();
        var ranges = bars.Select((_, i) =>
        {
            var previous = R(bars[Math.Max(0, i - 1)].Close); var candidates = new[] { R(highs[i]) - R(lows[i]), (R(highs[i]) - previous).Abs(), (R(lows[i]) - previous).Abs() };
            return RoundRocBankStage(candidates.Aggregate((a, b) => a.CompareTo(b) >= 0 ? a : b));
        }).ToArray();
        ReferenceFraction[] Mean(ReferenceFraction[] values) => kind is 1 or 2 or 3 or 6 ? SmoothRocBankStage(values, length, kind)
            : Average(values.Select(v => v.ToDouble()).ToArray(), length, kind).Select(R).ToArray();
        var volume = Mean(bars.Select(b => R(b.Volume)).ToArray()); var atr = Mean(ranges);
        ReferenceFraction[] Side(bool up) => Enumerable.Range(0, bars.Count).Select(i =>
        {
            for (var j = i; j >= 0; j--)
            {
                var divisor = up ? lows[j] : j == 0 ? 0 : lows[j - 1];
                if (atr[j].Sign <= 0 || volume[j].Sign == 0 || divisor == 0) continue;
                var numerator = up ? j == 0 ? 0 : highs[j - 1] : highs[j];
                return RoundRocBankStage(R(numerator) / (R(divisor) * volume[j] * root));
            }
            return zero;
        }).ToArray();
        var ups = Side(true); var downs = Side(false); var trades = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var spread = ups[i] - downs[i]; var previous = i == 0 ? zero : ups[i - 1] - downs[i - 1];
            trades[i] = spread.Sign > 0 && spread.CompareTo(previous) > 0 ? Signal.StrongBuy : spread.Sign < 0 && spread.CompareTo(previous) < 0 ? Signal.StrongSell : spread.Sign > 0 ? Signal.Buy : spread.Sign < 0 ? Signal.Sell : Signal.None;
        }
        return (new Dictionary<string, double[]> { ["KaseUp"] = ups.Select(v => v.ToDouble()).ToArray(), ["KaseDn"] = downs.Select(v => v.ToDouble()).ToArray() }, trades);
    }
}
