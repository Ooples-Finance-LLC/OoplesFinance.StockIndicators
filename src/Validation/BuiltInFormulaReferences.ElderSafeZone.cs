using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] ElderSafeZoneOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return ElderSafeZoneValues(bars, 63, Integer(options, "Length", 10), 3, AverageKind(options, 3), Number(options, 2.5, "Mult")).Values; }
    internal static (double[] Values, Signal[] Signals) ElderSafeZoneValues(IReadOnlyList<Bar> bars, int trendLength, int noiseLength, int stopLength, int kind, double factor, double[]? externalTrend = null)
    {
        trendLength = Math.Max(1, trendLength); noiseLength = Math.Max(1, noiseLength); stopLength = Math.Max(1, stopLength); ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var prices = bars.Select(b => R(b.Close)).ToArray(); var trend = externalTrend is null ? SmoothRocBankStage(prices, trendLength, kind) : externalTrend.Select(R).ToArray();
        var upward = bars.Select((b, i) => new[] { R(0), R(b.High) - R(i == 0 ? 0 : bars[i - 1].High) }.Max().RoundExtendedBinary64()).ToArray();
        var downward = bars.Select((b, i) => new[] { R(0), R(i == 0 ? 0 : bars[i - 1].Low) - R(b.Low) }.Max().RoundExtendedBinary64()).ToArray();
        ReferenceFraction Noise(ReferenceFraction[] moves, int i) { var events = Window(moves, i, noiseLength).Where(v => v.Sign > 0).ToArray(); return events.Length == 0 ? R(0) : (events.Aggregate(R(0), (a, b) => a + b) / R(events.Length)).RoundExtendedBinary64(); }
        var floor = bars.Select((_, i) => (R(i == 0 ? 0 : bars[i - 1].Low) - R(factor) * Noise(downward, i)).RoundExtendedBinary64()).ToArray();
        var ceiling = bars.Select((_, i) => (R(i == 0 ? 0 : bars[i - 1].High) + R(factor) * Noise(upward, i)).RoundExtendedBinary64()).ToArray();
        var values = new double[bars.Count]; var signals = new Signal[bars.Count]; var beforeBull = R(0); var beforeBear = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var stop = prices[i].CompareTo(trend[i]) >= 0 ? Window(floor, i, stopLength).Max() : Window(ceiling, i, stopLength).Min();
            var bullish = prices[i] - new[] { trend[i], stop }.Max(); var bearish = prices[i] - new[] { trend[i], stop }.Min();
            signals[i] = bullish.Sign > 0 && bullish.CompareTo(beforeBull) > 0 ? Signal.StrongBuy : bearish.Sign < 0 && bearish.CompareTo(beforeBear) < 0 ? Signal.StrongSell : bullish.Sign > 0 ? Signal.Buy : bearish.Sign < 0 ? Signal.Sell : Signal.None;
            values[i] = stop.ToDouble(); beforeBull = bullish; beforeBear = bearish;
        }
        return (values, signals);
    }
}
