using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> SellGravitationOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var o = indicator.CreateOptions(); return SellGravitationValues(bars, Integer(o, "Length", 20), (MovingAvgType)o.GetType().GetProperty("MaType")!.GetValue(o)!).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) SellGravitationValues(IReadOnlyList<Bar> bars, int length, MovingAvgType kind)
    {
        length = Math.Max(1, length); ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v); var zero = R(0);
        ReferenceFraction[] Mean(ReferenceFraction[] values) => RationalAverage(values, length, kind, (int)kind);
#pragma warning disable S1244 // Only an exactly zero candle range takes the zero-denominator convention; every nonzero range retains its ratio.
        var body = bars.Select(b => b.High == b.Low ? zero : (R(b.Close) - R(b.Open)) / (R(b.High) - R(b.Low))).ToArray();
#pragma warning restore S1244
        var line = Mean(body).Select(v => v.RoundExtendedBinary64()).ToArray();
        var signal = Mean(line); var trades = new Signal[bars.Count]; var previous = zero;
        for (var i = 0; i < bars.Count; i++)
        {
            var spread = line[i] - signal[i]; var change = spread.CompareTo(previous);
            trades[i] = spread.Sign > 0 && change > 0 ? Signal.StrongBuy : spread.Sign < 0 && change < 0 ? Signal.StrongSell : spread.Sign > 0 ? Signal.Buy : spread.Sign < 0 ? Signal.Sell : Signal.None;
            previous = spread;
        }
        return (new() { ["Sgi"] = line.Select(v => v.ToDouble()).ToArray(), ["Signal"] = signal.Select(v => v.ToDouble()).ToArray() }, trades);
    }
}
