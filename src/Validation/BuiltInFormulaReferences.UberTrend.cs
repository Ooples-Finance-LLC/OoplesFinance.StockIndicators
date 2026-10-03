using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> UberTrendOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
        => new Dictionary<string, double[]> { ["Uti"] = UberTrendValues(bars, Integer(indicator.CreateOptions(), "Length", 14)).Values };
    internal static (double[] Values, Signal[] Signals) UberTrendValues(IReadOnlyList<Bar> bars, int length)
    {
        length = Math.Max(1, length); ReferenceFraction R(double x) => ReferenceFraction.FromDouble(x); var zero = R(0);
        var upVolume = new ReferenceFraction[bars.Count]; var downVolume = new ReferenceFraction[bars.Count]; var values = new ReferenceFraction[bars.Count]; var signals = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var start = Math.Max(0, i - length + 1); var advance = zero; var decline = zero;
            for (var j = start; j <= i; j++) { var delta = j == 0 ? zero : R(bars[j].Close) - R(bars[j - 1].Close); if (delta.Sign > 0) advance += delta; else decline -= delta; }
            var change = i == 0 ? zero : R(bars[i].Close) - R(bars[i - 1].Close);
            upVolume[i] = change.Sign > 0 && advance.Sign != 0 ? R(bars[i].Volume) / advance : zero;
            downVolume[i] = change.Sign < 0 && decline.Sign != 0 ? R(bars[i].Volume) / decline : zero;
            var u = zero; var d = zero; for (var j = start; j <= i; j++) { u += upVolume[j]; d += downVolume[j]; }
            var top = decline.Sign == 0 ? zero : advance / decline; var bottom = d.Sign == 0 ? zero : u / d; var ratio = bottom.Sign == 0 ? zero : top / bottom;
            values[i] = (ratio + R(1)).Sign == 0 ? zero : (ratio - R(1)) / (ratio + R(1));
            var slope = values[i] - (i == 0 ? zero : values[i - 1]); var previousSlope = i == 0 ? zero : values[i - 1] - (i == 1 ? zero : values[i - 2]);
            signals[i] = slope.Sign > 0 && slope.CompareTo(previousSlope) > 0 ? Signal.StrongBuy : slope.Sign < 0 && slope.CompareTo(previousSlope) < 0 ? Signal.StrongSell : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        }
        return (values.Select(v => v.ToDouble()).ToArray(), signals);
    }
}
