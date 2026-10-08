using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> AdaptiveCandleOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); return AdaptiveCandleOutputs(bars, Math.Max(1, Integer(options, "SmoothLength", 5)), Math.Max(1, Integer(options, "StochLength", 14)), Math.Max(1, Integer(options, "SignalLength", 9)), AverageKind(options, 3));
    }
    internal static IReadOnlyDictionary<string, double[]> AdaptiveCandleOutputs(IReadOnlyList<Bar> bars, int smooth, int stoch, int signal, int kind)
    {
        var zero = new ReferenceFraction(0); var hundred = new ReferenceFraction(100); var one = new ReferenceFraction(1);
        var body = zero; var range = zero; var body2 = zero; var range2 = zero; var values = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var sample = bars.Skip(Math.Max(0, i - stoch + 1)).Take(Math.Min(i + 1, stoch)).ToArray();
            var high = ReferenceFraction.FromDouble(sample.Max(b => b.High)); var low = ReferenceFraction.FromDouble(sample.Min(b => b.Low));
            var price = ReferenceFraction.FromDouble(bars[i].Close); var span = high - low;
            var position = span.Sign == 0 ? zero : (price - low) / span;
            var k = Math.Max(0, Math.Min(100, (hundred * position).ToDouble()));
            var rate = ReferenceFraction.FromDouble((2 / (smooth + 1d)) * (Math.Abs(k - 50) / 50));
            var inputBody = (price - ReferenceFraction.FromDouble(bars[i].Open)).RoundExtendedBinary64();
            var inputRange = (ReferenceFraction.FromDouble(bars[i].High) - ReferenceFraction.FromDouble(bars[i].Low)).RoundExtendedBinary64();
            ReferenceFraction Step(ReferenceFraction previous, ReferenceFraction current) => i < 2L * (smooth + (long)stoch) ? current : ((one - rate) * previous + rate * current).RoundExtendedBinary64();
            body = Step(body, inputBody); range = Step(range, inputRange); body2 = Step(body2, body); range2 = Step(range2, range);
            values[i] = range2.Sign == 0 ? zero : ((body2 / range2).RoundExtendedBinary64() * hundred).RoundExtendedBinary64();
        }
        return Outputs(("Eco", values.Select(v => v.ToDouble()).ToArray()), ("Signal", SmoothRocBankStage(values, signal, kind).Select(v => v.ToDouble()).ToArray()));
    }
}
