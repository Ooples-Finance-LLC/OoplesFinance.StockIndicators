using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ChandelierOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); return ChandelierValues(bars, Integer(options, "Length", 22), AverageKind(options, 6), Number(options, 3, "Mult")).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) ChandelierValues(IReadOnlyList<Bar> bars, int length, int kind, double multiplier)
    {
        length = Math.Max(1, length); ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var ranges = bars.Select((b, i) => new[] { R(b.High) - R(b.Low), (R(b.High) - R(bars[i == 0 ? 0 : i - 1].Close)).Abs(), (R(b.Low) - R(bars[i == 0 ? 0 : i - 1].Close)).Abs() }.Max().RoundExtendedBinary64()).ToArray();
        var atr = SmoothRocBankStage(ranges, length, kind); var longs = new double[bars.Count]; var shorts = new double[bars.Count]; var signals = new Signal[bars.Count]; var previousBull = R(0); var previousBear = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var high = Window(bars, i, length).Max(b => b.High); var low = Window(bars, i, length).Min(b => b.Low);
            var distance = atr[i] * R(multiplier); var longStop = (R(high) - distance).RoundExtendedBinary64(); var shortStop = (R(low) + distance).RoundExtendedBinary64(); longs[i] = longStop.ToDouble(); shorts[i] = shortStop.ToDouble();
            var bull = R(bars[i].Close) - longStop; var bear = R(bars[i].Close) - shortStop;
            signals[i] = bull.Sign > 0 && bull.CompareTo(previousBull) > 0 ? Signal.StrongBuy : bear.Sign < 0 && bear.CompareTo(previousBear) < 0 ? Signal.StrongSell : bull.Sign > 0 ? Signal.Buy : bear.Sign < 0 ? Signal.Sell : Signal.None;
            previousBull = bull; previousBear = bear;
        }
        return (new Dictionary<string, double[]> { { "ExitLong", longs }, { "ExitShort", shorts } }, signals);
    }
}
