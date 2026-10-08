using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> UtBotOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); return UtBotValues(bars, Integer(options, "Length", 10), AverageKind(options, 6), Number(options, 1, "KeyValue")).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) UtBotValues(IReadOnlyList<Bar> bars, int length, int kind, double factor)
    {
        length = Math.Max(1, length); ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var prices = bars.Select(b => R(b.Close)).ToArray(); var ranges = bars.Select((b, i) => new[] { R(b.High) - R(b.Low), (R(b.High) - prices[i == 0 ? 0 : i - 1]).Abs(), (R(b.Low) - prices[i == 0 ? 0 : i - 1]).Abs() }.Max().RoundExtendedBinary64()).ToArray();
        var atr = SmoothRocBankStage(ranges, length, kind); var stops = new double[bars.Count]; var positions = new double[bars.Count]; var buys = new double[bars.Count]; var sells = new double[bars.Count]; var signals = new Signal[bars.Count]; var priorStop = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var previous = prices[i == 0 ? 0 : i - 1]; var side = Math.Sign(prices[i].CompareTo(priorStop)); var before = Math.Sign(previous.CompareTo(priorStop));
            var candidate = (prices[i] + R(side > 0 ? -1 : 1) * R(factor) * atr[i]).RoundExtendedBinary64();
            var choices = side != 0 && side == before ? new[] { candidate, priorStop } : new[] { candidate }; var stop = side > 0 ? choices.Max() : choices.Min();
            positions[i] = side * before < 0 ? side : i == 0 ? 0 : positions[i - 1];
            buys[i] = i > 0 && before <= 0 && prices[i].CompareTo(stop) > 0 ? 1 : 0; sells[i] = i > 0 && before >= 0 && prices[i].CompareTo(stop) < 0 ? 1 : 0;
            var difference = prices[i] - stop; var priorDifference = previous - priorStop;
            signals[i] = difference.Sign > 0 && difference.CompareTo(priorDifference) > 0 ? Signal.StrongBuy : difference.Sign < 0 && difference.CompareTo(priorDifference) < 0 ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None;
            stops[i] = stop.ToDouble(); priorStop = stop;
        }
        return (new Dictionary<string, double[]> { { "TrailingStop", stops }, { "Position", positions }, { "Buy", buys }, { "Sell", sells } }, signals);
    }
}
