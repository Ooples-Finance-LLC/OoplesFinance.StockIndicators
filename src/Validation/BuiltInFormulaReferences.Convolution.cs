using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ConvolutionOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
        => ConvolutionValues(bars, Integer(indicator.CreateOptions(), "Length1", 80), Integer(indicator.CreateOptions(), "Length2", 40), Integer(indicator.CreateOptions(), "Length3", 48)).Outputs;
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals, double[] Correlations) ConvolutionValues(IReadOnlyList<Bar> bars, int highLength, int lowLength, int correlationLength)
    {
        highLength = Math.Max(1, highLength); lowLength = Math.Max(1, lowLength); correlationLength = Math.Max(1, correlationLength);
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var angle = Math.Min(.99, Math.Sqrt(2) * Math.PI / highLength); var pole = R(Math.Cos(angle) / (1 + Math.Sin(angle)));
        var highGain = (R(1) + pole) / R(2); highGain *= highGain;
        var lowAngle = Math.Sqrt(2) * Math.PI / lowLength; var radius = R(Math.Exp(-lowAngle)); var cosine = R(Math.Cos(lowAngle));
        var gain = (R(1) - radius) * (R(1) - radius) + R(2) * radius * (R(1) - cosine);
        var high = new ReferenceFraction[bars.Count]; var roof = new ReferenceFraction[bars.Count];
        var values = new double[bars.Count]; var slopes = new double[bars.Count]; var correlations = new double[bars.Count]; var trades = new Signal[bars.Count];
        ReferenceFraction At(ReferenceFraction[] history, int i) => i < 0 ? R(0) : history[i];
        for (var i = 0; i < bars.Count; i++)
        {
            var change = R(bars[i].Close) - R(2) * (i == 0 ? R(0) : R(bars[i - 1].Close)) + (i < 2 ? R(0) : R(bars[i - 2].Close));
            high[i] = (highGain * change + R(2) * pole * At(high, i - 1) - pole * pole * At(high, i - 2)).RoundExtendedBinary64();
            roof[i] = (gain * (high[i] + At(high, i - 1)) / R(2) + R(2) * radius * cosine * At(roof, i - 1) - radius * radius * At(roof, i - 2)).RoundExtendedBinary64();
            var n = Math.Min(i + 1, correlationLength); var x = Enumerable.Range(0, n).Select(j => roof[i - j]).ToArray(); var y = Enumerable.Range(0, n).Select(j => At(roof, i - j - 1)).ToArray();
            var mx = x.Aggregate(R(0), (sum, v) => sum + v) / R(n); var my = y.Aggregate(R(0), (sum, v) => sum + v) / R(n);
            var covariance = x.Select((v, j) => (v - mx) * (y[j] - my)).Aggregate(R(0), (sum, v) => sum + v);
            var vx = x.Select(v => (v - mx) * (v - mx)).Aggregate(R(0), (sum, v) => sum + v); var vy = y.Select(v => (v - my) * (v - my)).Aggregate(R(0), (sum, v) => sum + v);
            correlations[i] = n < 2 || vx.Sign == 0 || vy.Sign == 0 ? 0 : covariance.Sign * (covariance * covariance / (vx * vy)).SqrtToDouble();
            var exponential = R(Math.Exp((R(3) * R(correlations[i])).ToDouble())); var denominator = R((exponential + R(1)).ToDouble());
            values[i] = (R((exponential / denominator).ToDouble()) / R(2)).ToDouble();
            var lag = (n + 1) / 2; var previous = At(roof, i - lag); var scale = new[] { R(1), roof[i].Abs(), previous.Abs() }.Max();
            slopes[i] = (roof[i] - previous).CompareTo(R(1e-12) * scale) > 0 ? -1 : 1;
            var changeNow = roof[i] - At(roof, i - 1); var changeBefore = At(roof, i - 1) - At(roof, i - 2);
            trades[i] = changeNow.Sign > 0 && changeNow.CompareTo(changeBefore) > 0 ? Signal.StrongBuy : changeNow.Sign < 0 && changeNow.CompareTo(changeBefore) < 0 ? Signal.StrongSell
                : changeNow.Sign > 0 ? Signal.Buy : changeNow.Sign < 0 ? Signal.Sell : Signal.None;
        }
        return (new Dictionary<string, double[]> { ["Eci"] = values, ["Slope"] = slopes }, trades, correlations);
    }
}
