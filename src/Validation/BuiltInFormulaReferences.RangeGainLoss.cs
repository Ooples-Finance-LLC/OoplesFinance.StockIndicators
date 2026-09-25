using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RangeGainLossOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var twice = indicator.BatchName == IndicatorName.DoubleSmoothedRelativeStrengthIndex;
        var kind = twice ? 3 : AverageKind(options, 3);
        var lookback = twice ? 2 : Integer(options, "Length1", 2);
        var periods = twice ? new[] { 5, 25 } : new[] { Integer(options, "Length2", 14) };
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var up = new ReferenceFraction[bars.Count]; var down = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var window = bars.Skip(Math.Max(0, i - lookback + 1)).Take(Math.Min(i + 1, lookback)).ToArray();
            up[i] = RoundStrengthStage(prices[i] - ReferenceFraction.FromDouble(window.Min(b => b.Close)));
            down[i] = RoundStrengthStage(ReferenceFraction.FromDouble(window.Max(b => b.Close)) - prices[i]);
        }
        foreach (var period in periods)
        {
            up = SmoothStrengthStage(up, period, kind); down = SmoothStrengthStage(down, period, kind);
        }
        var line = prices.Select((_, i) => (up[i] + down[i]).Sign == 0 ? 100 :
            (up[i] * new ReferenceFraction(100) / (up[i] + down[i])).ToDouble()).ToArray();
        var signal = SmoothStrengthStage(line.Select(ReferenceFraction.FromDouble).ToArray(), periods[periods.Length - 1], kind).Select(v => v.ToDouble()).ToArray();
        return Outputs((twice ? "Dsrsi" : "Mrsi", line), ("Signal", signal));
    }
}
