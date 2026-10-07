using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RmoOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
        => RmoValues(bars, 2, Integer(indicator.CreateOptions(), "Length", 10), 30, 81).Outputs;
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) RmoValues(IReadOnlyList<Bar> bars, int first, int range, int swing, int output)
    {
        first = Math.Max(1, first); range = Math.Max(2, range); swing = Math.Max(1, swing); output = Math.Max(1, output);
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var zero = R(0); var prices = bars.Select(b => R(b.Close)).ToArray();
        ReferenceFraction[] AverageExact(ReferenceFraction[] values, int period, bool ema)
        {
            var prefix = new ReferenceFraction[values.Length + 1]; prefix[0] = zero;
            for (var i = 0; i < values.Length; i++) prefix[i + 1] = prefix[i] + values[i];
            var result = new ReferenceFraction[values.Length];
            for (var i = 0; i < values.Length; i++)
                result[i] = !ema ? i + 1 < period ? zero : (prefix[i + 1] - prefix[Math.Max(0, i - period + 1)]) / R(period)
                    : i < period ? prefix[i + 1] / R(i + 1) : (result[i - 1] * R(period - 1) + R(2) * values[i]) / R(period + 1L);
            return result;
        }
        var cascades = new List<ReferenceFraction[]>(); var stage = prices;
        for (var pass = 0; pass < 10; pass++) { stage = AverageExact(stage, first, false); cascades.Add(stage); }
        var raw = new ReferenceFraction[prices.Length];
        for (var i = 0; i < prices.Length; i++)
        {
            var sample = bars.Skip(Math.Max(0, i - range + 1)).Take(Math.Min(i + 1, range)).ToArray();
            var width = R(sample.Max(b => b.Close)) - R(sample.Min(b => b.Close));
            var sum = zero; foreach (var values in cascades) sum += values[i];
            raw[i] = width.Sign == 0 ? zero : R(100) * (prices[i] - sum / R(10)) / width;
        }
        var second = AverageExact(raw, swing, true); var third = AverageExact(second, swing, true); var rmo = AverageExact(raw, output, true);
        var signals = new Signal[bars.Count];
        for (var i = 0; i < signals.Length; i++)
        {
            var value = rmo[i]; var previous = i == 0 ? zero : rmo[i - 1];
            signals[i] = value.Sign > 0 && value.CompareTo(previous) > 0 ? Signal.StrongBuy : value.Sign < 0 && value.CompareTo(previous) < 0 ? Signal.StrongSell
                : value.Sign > 0 ? Signal.Buy : value.Sign < 0 ? Signal.Sell : Signal.None;
        }
        return (new() { ["Rmo"] = rmo.Select(v => v.ToDouble()).ToArray(), ["SwingTrade1"] = raw.Select(v => v.ToDouble()).ToArray(),
            ["SwingTrade2"] = second.Select(v => v.ToDouble()).ToArray(), ["SwingTrade3"] = third.Select(v => v.ToDouble()).ToArray() }, signals);
    }
}
