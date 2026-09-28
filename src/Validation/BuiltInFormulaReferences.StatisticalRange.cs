using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> StatisticalRangeOutputs(IReadOnlyList<Bar> bars, int period, int annual, int kind)
    {
        period = Math.Max(1, period); annual = Math.Max(1, annual);
        var scale = Math.Sqrt((double)annual / period);
        var line = bars.Select((_, i) =>
        {
            var values = Window(bars, i, period).ToArray();
            var close = ReferenceSameSignLogRatio(values.Max(b => b.Close), values.Min(b => b.Close));
            var range = ReferenceSameSignLogRatio(values.Max(b => b.High), values.Min(b => b.Low));
            if (close < 0 && range > 0 || close > 0 && range < 0)
            {
                var ratio = ReferenceFraction.FromDouble(Math.Abs(values.Max(b => b.Close))) * ReferenceFraction.FromDouble(Math.Abs(values.Max(b => b.High)))
                    / (ReferenceFraction.FromDouble(Math.Abs(values.Min(b => b.Close))) * ReferenceFraction.FromDouble(Math.Abs(values.Min(b => b.Low))));
                close = ratio.LogToDouble(); range = 0;
            }
            return Math.Max(0, Math.Min(2.99, ((0.6 * close * scale) + (0.6 * range * scale)) * 0.5));
        }).ToArray();
        var signal = SmoothRocBankStage(line.Select(ReferenceFraction.FromDouble).ToArray(), period, kind);
        return new() { { "Sv", line }, { "Signal", signal.Select(v => v.ToDouble()).ToArray() } };
    }
}
