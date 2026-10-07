using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] SuperTrendOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return SuperTrendValues(bars, Integer(options, "Length", 22), AverageKind(options, 6), 3).Values; }
    internal static (double[] Values, Signal[] Signals) SuperTrendValues(IReadOnlyList<Bar> bars, int length, int kind, double factor)
    {
        length = Math.Max(1, length); ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var prices = bars.Select(b => R(b.Close)).ToArray(); var ranges = bars.Select((b, i) => new[] { R(b.High) - R(b.Low), (R(b.High) - prices[i == 0 ? 0 : i - 1]).Abs(), (R(b.Low) - prices[i == 0 ? 0 : i - 1]).Abs() }.Max().RoundExtendedBinary64()).ToArray();
        var atr = SmoothRocBankStage(ranges, length, kind); var lower = prices.Select((v, i) => (v - R(factor) * atr[i]).RoundExtendedBinary64()).ToArray(); var upper = prices.Select((v, i) => (v + R(factor) * atr[i]).RoundExtendedBinary64()).ToArray();
        var values = new double[bars.Count]; var signals = new Signal[bars.Count]; var lowerStart = 0; var upperStart = 0; var bullish = true; var priorTrend = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var previousLower = i == 0 ? lower[i] : lower.Skip(lowerStart).Take(i - lowerStart).Max(); var previousUpper = i == 0 ? upper[i] : upper.Skip(upperStart).Take(i - upperStart).Min();
            if (bullish && prices[i].CompareTo(previousLower) < 0) bullish = false; else if (!bullish && prices[i].CompareTo(previousUpper) > 0) bullish = true;
            var previous = i == 0 ? R(0) : prices[i - 1]; if (previous.CompareTo(previousLower) <= 0) lowerStart = i; if (previous.CompareTo(previousUpper) >= 0) upperStart = i;
            var trend = bullish ? lower.Skip(lowerStart).Take(i - lowerStart + 1).Max() : upper.Skip(upperStart).Take(i - upperStart + 1).Min();
            var difference = prices[i] - trend; var before = previous - priorTrend;
            signals[i] = difference.Sign > 0 && difference.CompareTo(before) > 0 ? Signal.StrongBuy : difference.Sign < 0 && difference.CompareTo(before) < 0 ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None;
            values[i] = trend.ToDouble(); priorTrend = trend;
        }
        return (values, signals);
    }
}
