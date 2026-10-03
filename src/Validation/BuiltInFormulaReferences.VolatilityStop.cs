using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) VolatilityStopValues(IReadOnlyList<Bar> bars, int length, double factor)
    {
        length = Math.Max(1, length); ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var prices = bars.Select(b => R(b.Close)).ToArray(); var ranges = bars.Select((b, i) => new[] { R(b.High) - R(b.Low), (R(b.High) - prices[i == 0 ? 0 : i - 1]).Abs(), (R(b.Low) - prices[i == 0 ? 0 : i - 1]).Abs() }.Max().RoundExtendedBinary64()).ToArray();
        var atr = SmoothRocBankStage(ranges, length, 6); var values = new double[bars.Count]; var signals = new Signal[bars.Count]; var previousDifference = R(0); var priorStop = R(0); var candidates = new List<ReferenceFraction>(); var direction = 1;
        for (var i = 0; i < bars.Count; i++)
        {
            if (i == 0) candidates.Add(prices[i]);
            else
            {
                if (direction * prices[i].CompareTo(priorStop) < 0) { direction = -direction; candidates.Clear(); }
                candidates.Add((prices[i] - R(direction) * R(factor) * atr[i]).RoundExtendedBinary64());
            }
            var stop = direction > 0 ? candidates.Max() : candidates.Min(); values[i] = stop.ToDouble(); var difference = prices[i] - stop;
            signals[i] = difference.Sign > 0 && difference.CompareTo(previousDifference) > 0 ? Signal.StrongBuy : difference.Sign < 0 && difference.CompareTo(previousDifference) < 0 ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None;
            priorStop = stop; previousDifference = difference;
        }
        return (new Dictionary<string, double[]> { { "Vs", values } }, signals);
    }
}
