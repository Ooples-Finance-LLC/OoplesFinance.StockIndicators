using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ChandeKrollOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return ChandeKrollValues(bars, Integer(options, "Length", 14), 3, AverageKind(options, 1)).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, double[] Raw, Signal[] Signals) ChandeKrollValues(IReadOnlyList<Bar> bars, int length, int smoothLength, int kind, double[]? external = null)
    {
        length = Math.Max(1, length); smoothLength = Math.Max(1, smoothLength); ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var raw = new double[bars.Count]; var signals = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var window = Window(bars, i, length).Select(b => R(b.Close)).ToArray(); if (window.Length < 2) continue;
            var mean = window.Aggregate(R(0), (a, b) => a + b) / new ReferenceFraction(window.Length); var center = new ReferenceFraction(window.Length - 1) / R(2);
            var covariance = Enumerable.Range(0, window.Length).Aggregate(R(0), (a, j) => a + (R(j) - center) * (window[j] - mean));
            var priceVariance = window.Aggregate(R(0), (a, b) => a + (b - mean) * (b - mean));
            var timeVariance = Enumerable.Range(0, window.Length).Aggregate(R(0), (a, j) => a + (R(j) - center) * (R(j) - center));
            var correlation = priceVariance.Sign == 0 ? 0 : (covariance * covariance / (priceVariance * timeVariance)).SqrtToDouble(); raw[i] = (R(correlation) * R(correlation)).ToDouble();
        }
        var values = external ?? SmoothRocBankStage(raw.Select(R).ToArray(), smoothLength, kind).Select(v => v.ToDouble()).ToArray(); var previous = R(0); var previousSlope = R(0);
        for (var i = 0; i < values.Length; i++) { var slope = R(values[i]) - previous; signals[i] = slope.Sign > 0 && slope.CompareTo(previousSlope) > 0 ? Signal.StrongBuy : slope.Sign < 0 && slope.CompareTo(previousSlope) < 0 ? Signal.StrongSell : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None; previous = R(values[i]); previousSlope = slope; }
        return (new Dictionary<string, double[]> { ["Ckrsi"] = values }, raw, signals);
    }
}
