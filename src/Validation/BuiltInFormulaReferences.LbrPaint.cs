using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> LbrPaintOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); return LbrPaintValues(bars, Integer(o, "Length", 9), Integer(o, "LbLength", 16), Number(o, 2.5, "AtrMult"), AverageKind(o, 1)).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) LbrPaintValues(IReadOnlyList<Bar> bars, int length, int lookback, double multiplier, int kind)
    {
        length = Math.Max(1, length); lookback = Math.Max(1, lookback);
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var ranges = bars.Select((b, i) => new[] { R(b.High) - R(b.Low), (R(b.High) - R(i == 0 ? b.Close : bars[i - 1].Close)).Abs(), (R(b.Low) - R(i == 0 ? b.Close : bars[i - 1].Close)).Abs() }
            .Aggregate((a, c) => a.CompareTo(c) >= 0 ? a : c).RoundExtendedBinary64()).ToArray();
        var means = SmoothRocBankStage(ranges, length, kind); var upper = new double[bars.Count]; var lower = new double[bars.Count]; var width = new double[bars.Count]; var trades = new Signal[bars.Count];
        var previousBull = R(0); var previousBear = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var window = Window(bars, i, lookback).ToArray(); var distance = means[i] * R(multiplier);
            var top = R(window.Max(b => b.High)) - distance; var bottom = R(window.Min(b => b.Low)) + distance;
            var maximum = top.CompareTo(bottom) >= 0 ? top : bottom; var minimum = top.CompareTo(bottom) >= 0 ? bottom : top;
            var bull = R(bars[i].Close) - maximum; var bear = R(bars[i].Close) - minimum;
            trades[i] = bull.Sign > 0 && bull.CompareTo(previousBull) > 0 ? Signal.StrongBuy : bear.Sign < 0 && bear.CompareTo(previousBear) < 0 ? Signal.StrongSell
                : bull.Sign > 0 ? Signal.Buy : bear.Sign < 0 ? Signal.Sell : Signal.None;
            upper[i] = top.ToDouble(); lower[i] = bottom.ToDouble(); width[i] = distance.ToDouble(); previousBull = bull; previousBear = bear;
        }
        return (new Dictionary<string, double[]> { ["UpperBand"] = upper, ["LowerBand"] = lower, ["Aatr"] = width }, trades);
    }
}
