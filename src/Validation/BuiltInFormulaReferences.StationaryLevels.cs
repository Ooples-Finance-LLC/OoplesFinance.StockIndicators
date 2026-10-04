using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> StationaryLevelsOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var o = indicator.CreateOptions(); return StationaryLevelsValues(bars, Integer(o, "Length", 200), (MovingAvgType)o.GetType().GetProperty("MaType")!.GetValue(o)!).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) StationaryLevelsValues(IReadOnlyList<Bar> bars, int length, MovingAvgType kind)
    {
        length = Math.Max(1, length); ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v); var zero = R(0);
        ReferenceFraction[] Mean(ReferenceFraction[] values) => RationalAverage(values, length, kind);
        var prices = bars.Select(b => R(b.Close)).ToArray(); var mean = Mean(prices);
        var residual = prices.Select((v, i) => v - mean[i]).ToArray(); var ext = new ReferenceFraction[bars.Count];
        for (var i = 0; i < ext.Length; i++)
        {
            var firstIndex = i >= length ? i - length : 0; var secondIndex = i >= 2L * length ? i - 2L * length : 0;
            var first = i >= length ? residual[i - length] : zero; var second = i >= 2L * length ? residual[(int)(i - 2L * length)] : zero;
            ext[i] = firstIndex == secondIndex || first.CompareTo(second) == 0 ? zero
                : (first + (R(i) - R(firstIndex)) / (R(secondIndex) - R(firstIndex)) * (second - first)) / R(2);
        }
        ReferenceFraction[] Extreme(ReferenceFraction[] values, int width, bool high)
        {
            var result = new ReferenceFraction[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                var best = values[i];
                for (var j = Math.Max(0, i - width + 1); j < i; j++)
                    if (high ? values[j].CompareTo(best) > 0 : values[j].CompareTo(best) < 0) best = values[j];
                result[i] = best;
            }
            return result;
        }
        var upper = Extreme(Extreme(ext, Math.Max(2, length), true), length, true);
        var lower = Extreme(Extreme(ext, Math.Max(2, length), false), length, false);
        var middle = upper.Select((v, i) => (v + lower[i]) / R(2)).ToArray(); var trades = new Signal[bars.Count]; var previous = zero;
        for (var i = 0; i < trades.Length; i++)
        {
            var comparison = residual[i] - ext[i]; var change = comparison.CompareTo(previous);
            trades[i] = comparison.Sign > 0 && change > 0 ? Signal.StrongBuy : comparison.Sign < 0 && change < 0 ? Signal.StrongSell
                : comparison.Sign > 0 ? Signal.Buy : comparison.Sign < 0 ? Signal.Sell : Signal.None;
            previous = comparison;
        }
        return (new() { ["UpperBand"] = upper.Select(v => v.ToDouble()).ToArray(), ["MiddleBand"] = middle.Select(v => v.ToDouble()).ToArray(), ["LowerBand"] = lower.Select(v => v.ToDouble()).ToArray(), ["Deviation"] = residual.Select(v => v.ToDouble()).ToArray() }, trades);
    }
}
