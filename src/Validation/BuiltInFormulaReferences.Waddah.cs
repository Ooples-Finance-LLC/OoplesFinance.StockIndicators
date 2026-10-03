using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> WaddahOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return WaddahValues(bars, Integer(options, "FastLength", 20), Integer(options, "SlowLength", 40), Number(options, 150, "Sensitivity")).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) WaddahValues(IReadOnlyList<Bar> bars, int fast, int slow, double sensitivity)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var threshold = R(fast); fast = Math.Max(1, fast); slow = Math.Max(1, slow);
        var prices = bars.Select(b => R(b.Close)).ToArray();
        ReferenceFraction[] Macd(int lag)
        {
            var values = prices.Select((_, i) => i < lag ? R(0) : prices[i - lag]).ToArray();
            var first = SmoothRocBankStage(values, fast, 3, v => v); var second = SmoothRocBankStage(values, slow, 3, v => v);
            return first.Zip(second, (a, b) => a - b).ToArray();
        }
        var a = Macd(0); var b = Macd(1); var c = Macd(2); var d = Macd(3); var multiplier = R(sensitivity);
        var current = a.Zip(b, (x, y) => (x - y) * multiplier).ToArray(); var lagged = c.Zip(d, (x, y) => (x - y) * multiplier).ToArray();
        var square = prices.Select((_, i) =>
        {
            if (i + 1 < fast) return R(0);
            var window = prices.Skip(i - fast + 1).Take(fast).ToArray(); var mean = window.Aggregate(R(0), (x, y) => x + y) / R(fast);
            return window.Aggregate(R(0), (sum, value) => sum + (value - mean) * (value - mean)) * R(16) / R(fast);
        }).ToArray();
        var up = current.Select(v => v.Sign > 0 ? v : R(0)).ToArray(); var down = current.Select(v => v.Sign < 0 ? R(0) - v : R(0)).ToArray();
        var signals = current.Select((_, i) =>
        {
            var previousUp = i == 0 ? R(0) : up[i - 1]; var previousSquare = i == 0 ? R(0) : square[i - 1];
            var buy = up[i].CompareTo(previousUp) > 0 && (up[i] * up[i]).CompareTo(square[i]) > 0 && square[i].CompareTo(previousSquare) > 0 &&
                up[i].CompareTo(threshold) > 0 && (threshold.Sign < 0 || square[i].CompareTo(threshold * threshold) > 0);
            return buy ? Signal.Buy : (up[i] * up[i]).CompareTo(square[i]) < 0 ? Signal.Sell : Signal.None;
        }).ToArray();
        return (new Dictionary<string, double[]> { ["T1"] = current.Select(v => v.ToDouble()).ToArray(), ["T2"] = lagged.Select(v => v.ToDouble()).ToArray(),
            ["E1"] = square.Select(v => v.SqrtToDouble()).ToArray(), ["TrendUp"] = up.Select(v => v.ToDouble()).ToArray(), ["TrendDn"] = down.Select(v => v.ToDouble()).ToArray() }, signals);
    }
}
